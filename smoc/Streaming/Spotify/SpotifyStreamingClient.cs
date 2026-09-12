using Terminal.Gui.App;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Smoc.Configuration;
using Smoc.Services;
using Smoc.Services.Caching;
using Smoc.Streaming.Spotify.Util;
using SpotifyAPI.Web;

namespace Smoc.Streaming.Spotify;

/// <summary>
/// A streaming client implementation for Spotify.
/// </summary>
public sealed class SpotifyStreamingClient : IStreamingClient, IDisposable {
  private readonly ICacheService _songCacheService;
  private readonly ICacheService _albumArtCacheService;
  private readonly HttpClient _httpClient;
  private readonly bool _disposeHttpClient;
  private readonly SemaphoreSlim _initLock = new(1, 1);
  private ISpotifyClient? _spotifyClient;
  private bool _isDisposed;

  private SpotifyStreamingClient(
      ICacheService? songCacheService = null,
      ICacheService? albumArtCacheService = null,
      ISpotifyClient? spotifyClient = null,
      HttpClient? httpClient = null) {
    _songCacheService = songCacheService ?? new NoCachingCacheService();
    _albumArtCacheService = albumArtCacheService ?? new NoCachingCacheService();
    _spotifyClient = spotifyClient;
    if (httpClient != null) {
      _httpClient = httpClient;
      _disposeHttpClient = false;
    } else {
      _httpClient = new HttpClient();
      _disposeHttpClient = true;
    }
  }

  /// <summary>
  /// Creates a new instance of <see cref="SpotifyStreamingClient"/>.
  /// </summary>
  /// <param name="songCacheService">Optional song cache service.</param>
  /// <param name="albumArtCacheService">Optional album art cache service.</param>
  /// <returns>A new <see cref="SpotifyStreamingClient"/> instance.</returns>
  public static SpotifyStreamingClient Create(ICacheService? songCacheService = null, ICacheService? albumArtCacheService = null) {
    return new SpotifyStreamingClient(songCacheService, albumArtCacheService);
  }

  /// <summary>
  /// Creates a new instance of <see cref="SpotifyStreamingClient"/> for testing with a mock or custom Spotify client.
  /// </summary>
  /// <param name="spotifyClient">The <see cref="ISpotifyClient"/> instance to use.</param>
  /// <param name="songCacheService">Optional song cache service.</param>
  /// <param name="albumArtCacheService">Optional album art cache service.</param>
  /// <param name="httpClient">Optional HTTP client.</param>
  /// <returns>A new <see cref="SpotifyStreamingClient"/> instance.</returns>
  public static SpotifyStreamingClient CreateForTesting(
      ISpotifyClient spotifyClient,
      ICacheService? songCacheService = null,
      ICacheService? albumArtCacheService = null,
      HttpClient? httpClient = null) {
    return new SpotifyStreamingClient(songCacheService, albumArtCacheService, spotifyClient, httpClient);
  }

  private async Task EnsureSpotifyClientAsync(CancellationToken cancellationToken = default) {
    if (_spotifyClient != null) {
      return;
    }

    await _initLock.WaitAsync(cancellationToken);
    try {
      if (_spotifyClient != null) {
        return;
      }

      if (string.IsNullOrEmpty(SpotifyConfig.Defaults.ClientId) || string.IsNullOrEmpty(SpotifyConfig.Defaults.ClientSecret)) {
        Logging.Error("Spotify Client ID and Client Secret must be configured.");
        throw new InvalidOperationException("Spotify Client ID and Client Secret must be configured.");
      }

      var config = SpotifyClientConfig.CreateDefault()
        .WithAuthenticator(new ClientCredentialsAuthenticator(
          SpotifyConfig.Defaults.ClientId!,
          SpotifyConfig.Defaults.ClientSecret!));
      _spotifyClient = new SpotifyClient(config);
    } finally {
      _initLock.Release();
    }
  }

  /// <inheritdoc/>
  public async Task<List<Artist>> SearchArtistsAsync(string query, CancellationToken cancellationToken = default) {
    await EnsureSpotifyClientAsync(cancellationToken);
    var searchRequest = new SearchRequest(SearchRequest.Types.Artist, query);
    var searchResponse = await _spotifyClient!.Search.Item(searchRequest, cancellationToken);
    return searchResponse.Artists?.Items?.Select(SpotifyMapper.MapArtist).ToList() ?? [];
  }

  /// <inheritdoc/>
  public async Task<List<Song>> SearchSongsAsync(string query, CancellationToken cancellationToken = default) {
    await EnsureSpotifyClientAsync(cancellationToken);
    var searchRequest = new SearchRequest(SearchRequest.Types.Track, query);
    var searchResponse = await _spotifyClient!.Search.Item(searchRequest, cancellationToken);
    return searchResponse.Tracks?.Items?.Select(SpotifyMapper.MapTrackToSong).ToList() ?? [];
  }

  /// <inheritdoc/>
  public async Task<Song> GetSongAsync(string songId, CancellationToken cancellationToken = default) {
    await EnsureSpotifyClientAsync(cancellationToken);
    var track = await _spotifyClient!.Tracks.Get(songId, cancellationToken);
    return SpotifyMapper.MapTrackToSong(track);
  }

  /// <inheritdoc/>
  public async Task<Artist> GetArtistAsync(string artistId, CancellationToken cancellationToken = default) {
    await EnsureSpotifyClientAsync(cancellationToken);
    var artist = await _spotifyClient!.Artists.Get(artistId, cancellationToken);
    return SpotifyMapper.MapArtist(artist);
  }

  /// <inheritdoc/>
  public async Task<List<Album>> GetAlbumsByArtistAsync(Artist artist, CancellationToken cancellationToken = default) {
    await EnsureSpotifyClientAsync(cancellationToken);
    var albums = await _spotifyClient!.Artists.GetAlbums(artist.Id, cancellationToken);
    return albums.Items?.Select(a => SpotifyMapper.MapSimpleAlbumToAlbum(a, artist)).ToList() ?? [];
  }

  /// <inheritdoc/>
  public async Task<List<Song>> GetSongsByAlbumAsync(Album album, CancellationToken cancellationToken = default) {
    await EnsureSpotifyClientAsync(cancellationToken);
    var albumTracks = await _spotifyClient!.Albums.GetTracks(album.Id, cancellationToken);
    return albumTracks.Items?.Select(t => SpotifyMapper.MapSimpleTrackToSong(t, album)).ToList() ?? [];
  }

  /// <inheritdoc/>
  public Task<SongStream> GetSongStreamAsync(string songId, CancellationToken cancellationToken = default) {
    throw new NotSupportedException("Spotify playback is not supported yet (requires Librespot integration). Metadata and search only.");
  }

  /// <inheritdoc/>
  public Task<List<Song>> GetLikedSongsAsync(CancellationToken cancellationToken = default) {
    // Requires User Token
    return Task.FromResult<List<Song>>([]);
  }

  /// <inheritdoc/>
  public async Task<List<Playlist>> SearchPlaylistsAsync(string query, CancellationToken cancellationToken = default) {
    await EnsureSpotifyClientAsync(cancellationToken);
    var searchRequest = new SearchRequest(SearchRequest.Types.Playlist, query);
    var searchResponse = await _spotifyClient!.Search.Item(searchRequest, cancellationToken);
    return searchResponse.Playlists?.Items?.Select(p => new Playlist(p.Id ?? string.Empty, p.Name ?? string.Empty)).ToList() ?? [];
  }

  /// <inheritdoc/>
  public async Task<List<Song>> GetPlaylistSongsAsync(Playlist playlist, CancellationToken cancellationToken = default) {
    await EnsureSpotifyClientAsync(cancellationToken);
    var playlistTracks = await _spotifyClient!.Playlists.GetPlaylistItems(playlist.Id, cancellationToken);
    return playlistTracks.Items?
      .Where(i => i?.Track is FullTrack)
      .Select(i => SpotifyMapper.MapTrackToSong((FullTrack)i.Track!))
      .ToList() ?? [];
  }

  /// <inheritdoc/>
  public async Task<List<Song>> GetPlaylistSongsFromUrlAsync(string url, CancellationToken cancellationToken = default) {
    var (entityType, id) = SpotifyUrlParser.ParseUrl(url);

    if (entityType == typeof(Song)) {
      return [await GetSongAsync(id, cancellationToken)];
    }

    if (entityType == typeof(Playlist)) {
      return await GetPlaylistSongsAsync(new Playlist(id, string.Empty), cancellationToken);
    }

    if (entityType == typeof(Album)) {
      await EnsureSpotifyClientAsync(cancellationToken);
      var fullAlbum = await _spotifyClient!.Albums.Get(id, cancellationToken);
      var artist = fullAlbum.Artists?.Count > 0 ? SpotifyMapper.MapArtist(fullAlbum.Artists[0]) : new Artist(string.Empty, "Unknown Artist");
      var album = SpotifyMapper.MapFullAlbumToAlbum(fullAlbum, artist);
      var albumTracks = await _spotifyClient!.Albums.GetTracks(id, cancellationToken);
      return albumTracks.Items?.Select(t => SpotifyMapper.MapSimpleTrackToSong(t, album)).ToList() ?? [];
    }

    return [];
  }

  /// <inheritdoc/>
  public Task AddToListenHistory(Song song, CancellationToken cancellationToken = default) {
    return Task.CompletedTask;
  }

  /// <inheritdoc/>
  public async Task<Image<Rgba32>> GetAlbumArtAsync(Album album, Func<IEnumerable<AlbumCover>, AlbumCover>? coverSelector = null, CancellationToken cancellationToken = default) {
    if (!album.Covers.Any()) {
      throw new ArgumentException("Album has no covers.", nameof(album));
    }

    var cover = coverSelector?.Invoke(album.Covers) ?? album.Covers.First();

    using var albumArt = await _albumArtCacheService.GetOrAddAsync(
      string.Concat(album.Id, "-", cover.Width, "x", cover.Height),
      async ct => {
        var albumResponse = await _httpClient.GetAsync(cover.Url, ct);
        return await albumResponse.Content.ReadAsStreamAsync(ct);
      },
      cancellationToken);
    return await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(albumArt, cancellationToken);
  }

  /// <inheritdoc/>
  public void Dispose() {
    if (_isDisposed) {
      return;
    }

    _initLock.Dispose();
    if (_disposeHttpClient) {
      _httpClient.Dispose();
    }

    _isDisposed = true;
  }
}
