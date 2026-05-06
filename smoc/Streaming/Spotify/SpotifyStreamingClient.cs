using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Smoc.Configuration;
using Smoc.Services;
using Smoc.Services.Caching;
using SpotifyAPI.Web;
using Terminal.Gui.App;

namespace Smoc.Streaming.Spotify;

/// <summary>
/// A streaming client implementation for Spotify.
/// </summary>
public sealed class SpotifyStreamingClient : IStreamingClient, IDisposable {
  private readonly ICacheService _songCacheService;
  private readonly ICacheService _albumArtCacheService;
  private readonly HttpClient _httpClient;
  private SpotifyClient? _spotifyClient;
  private bool _isDisposed;

  private SpotifyStreamingClient(ICacheService? songCacheService = null, ICacheService? albumArtCacheService = null) {
    _songCacheService = songCacheService ?? new NoCachingCacheService();
    _albumArtCacheService = albumArtCacheService ?? new NoCachingCacheService();
    _httpClient = new HttpClient();
  }

  public static SpotifyStreamingClient Create(ICacheService? songCacheService = null, ICacheService? albumArtCacheService = null) {
    return new SpotifyStreamingClient(songCacheService, albumArtCacheService);
  }

  private async Task EnsureSpotifyClientAsync() {
    if (_spotifyClient != null) return;

    if (string.IsNullOrEmpty(SpotifyConfig.ClientId) || string.IsNullOrEmpty(SpotifyConfig.ClientSecret)) {
      Logging.Error("Spotify Client ID and Client Secret must be configured.");
      throw new InvalidOperationException("Spotify Client ID and Client Secret must be configured.");
    }

    var config = SpotifyClientConfig.CreateDefault();
    var request = new ClientCredentialsRequest(SpotifyConfig.ClientId, SpotifyConfig.ClientSecret);
    var oauthClient = new OAuthClient(config);
    var response = await oauthClient.RequestToken(request);

    _spotifyClient = new SpotifyClient(config.WithToken(response.AccessToken));
  }

  /// <inheritdoc/>
  public async Task<List<Artist>> SearchArtistsAsync(string query, CancellationToken cancellationToken = default) {
    await EnsureSpotifyClientAsync();
    var searchRequest = new SearchRequest(SearchRequest.Types.Artist, query);
    var searchResponse = await _spotifyClient!.Search.Item(searchRequest, cancellationToken);
    return searchResponse.Artists.Items?.Select(a => new Artist(a.Id, a.Name)).ToList() ?? [];
  }

  /// <inheritdoc/>
  public async Task<List<Song>> SearchSongsAsync(string query, CancellationToken cancellationToken = default) {
    await EnsureSpotifyClientAsync();
    var searchRequest = new SearchRequest(SearchRequest.Types.Track, query);
    var searchResponse = await _spotifyClient!.Search.Item(searchRequest, cancellationToken);
    return searchResponse.Tracks.Items?.Select(MapTrackToSong).ToList() ?? [];
  }

  /// <inheritdoc/>
  public async Task<Song> GetSongAsync(string songId, CancellationToken cancellationToken = default) {
    await EnsureSpotifyClientAsync();
    var track = await _spotifyClient!.Tracks.Get(songId, cancellationToken);
    return MapTrackToSong(track);
  }

  /// <inheritdoc/>
  public async Task<Artist> GetArtistAsync(string artistId, CancellationToken cancellationToken = default) {
    await EnsureSpotifyClientAsync();
    var artist = await _spotifyClient!.Artists.Get(artistId, cancellationToken);
    return new Artist(artist.Id, artist.Name);
  }

  /// <inheritdoc/>
  public async Task<List<Album>> GetAlbumsByArtistAsync(Artist artist, CancellationToken cancellationToken = default) {
    await EnsureSpotifyClientAsync();
    var albums = await _spotifyClient!.Artists.GetAlbums(artist.Id);
    return albums.Items?.Select(a => MapSimpleAlbumToAlbum(a, artist)).ToList() ?? [];
  }

  /// <inheritdoc/>
  public async Task<List<Song>> GetSongsByAlbumAsync(Album album, CancellationToken cancellationToken = default) {
    await EnsureSpotifyClientAsync();
    var albumTracks = await _spotifyClient!.Albums.GetTracks(album.Id);
    return albumTracks.Items?.Select(t => MapSimpleTrackToSong(t, album)).ToList() ?? [];
  }

  /// <inheritdoc/>
  public async Task<SongStream> GetSongStreamAsync(string songId, CancellationToken cancellationToken = default) {
    // TODO: Implement Librespot playback logic once the dependency is resolved.
    // For now, we throw a descriptive exception.
    throw new NotImplementedException("Spotify playback requires Librespot-DotNet which is currently being integrated.");
  }

  /// <inheritdoc/>
  public async Task<List<Song>> GetLikedSongsAsync(CancellationToken cancellationToken = default) {
    // Requires User Token
    return [];
  }

  /// <inheritdoc/>
  public async Task<List<Playlist>> SearchPlaylistsAsync(string query, CancellationToken cancellationToken = default) {
    await EnsureSpotifyClientAsync();
    var searchRequest = new SearchRequest(SearchRequest.Types.Playlist, query);
    var searchResponse = await _spotifyClient!.Search.Item(searchRequest, cancellationToken);
    return searchResponse.Playlists.Items?.Select(p => new Playlist(p.Id ?? string.Empty, p.Name ?? string.Empty)).ToList() ?? [];
  }

  /// <inheritdoc/>
    /// <inheritdoc/>
  public async Task<List<Song>> GetPlaylistSongsAsync(Playlist playlist, CancellationToken cancellationToken = default) {
    await EnsureSpotifyClientAsync();
    var playlistTracks = await _spotifyClient!.Playlists.GetPlaylistItems(playlist.Id);
    return playlistTracks.Items?
        .Where(i => i.Track is FullTrack)
        .Select(i => MapTrackToSong((FullTrack)i.Track))
        .ToList() ?? [];
  }

  /// <inheritdoc/>
  public async Task<List<Song>> GetPlaylistSongsFromUrlAsync(string url, CancellationToken cancellationToken = default) {
    // Simple URL parsing logic
    if (url.Contains("track/")) {
        var id = url.Split("track/").Last().Split('?').First();
        return [await GetSongAsync(id, cancellationToken)];
    }
    if (url.Contains("playlist/")) {
        var id = url.Split("playlist/").Last().Split('?').First();
        return await GetPlaylistSongsAsync(new Playlist(id, ""), cancellationToken);
    }
    return [];
  }

  /// <inheritdoc/>
  public async Task AddToListenHistory(Song song, CancellationToken cancellationToken = default) {
    await Task.CompletedTask;
  }

  /// <inheritdoc/>
  public async Task<Image<Rgba32>> GetAlbumArtAsync(Album album, Func<IEnumerable<AlbumCover>, AlbumCover>? coverSelector = null, CancellationToken cancellationToken = default) {
    if (!album.Covers.Any())
      throw new ArgumentException("Album has no covers.", nameof(album));

    var cover = coverSelector?.Invoke(album.Covers) ?? album.Covers.First();

    using var albumArt = await _albumArtCacheService.GetOrAddAsync(
      string.Concat(album.Id, "-", cover.Width, "x", cover.Height),
      async ct => {
        var albumResponse = await _httpClient.GetAsync(cover.Url, cancellationToken);
        return await albumResponse.Content.ReadAsStreamAsync(cancellationToken);
      },
      cancellationToken);
    return await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(albumArt, cancellationToken);
  }

  private Song MapTrackToSong(FullTrack track) {
    var artist = new Artist(track.Artists.First().Id, track.Artists.First().Name);
    var album = MapSimpleAlbumToAlbum(track.Album, artist);
    return new Song(track.Id, album, track.Name, TimeSpan.FromMilliseconds(track.DurationMs), track.TrackNumber);
  }

  private Song MapSimpleTrackToSong(SimpleTrack track, Album album) {
    return new Song(track.Id, album, track.Name, TimeSpan.FromMilliseconds(track.DurationMs), track.TrackNumber);
  }

  private Album MapSimpleAlbumToAlbum(SimpleAlbum album, Artist artist) {
    int? releaseYear = null;
    if (!string.IsNullOrEmpty(album.ReleaseDate) && album.ReleaseDate.Length >= 4 && int.TryParse(album.ReleaseDate.Substring(0, 4), out var year)) {
      releaseYear = year;
    }

    return new Album(
        album.Id,
        artist,
        album.Name,
        album.Images.Select(i => new AlbumCover(i.Url, i.Width, i.Height)),
        releaseYear);
  }

  public void Dispose() {
    if (_isDisposed) return;
    _httpClient.Dispose();
    _isDisposed = true;
  }
}