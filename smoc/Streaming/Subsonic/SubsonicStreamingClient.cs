using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Smoc.Configuration;
using Smoc.Services;
using Smoc.Services.Caching;
using Smoc.Streaming.Subsonic.Util;

namespace Smoc.Streaming.Subsonic;

using SubsonicModels = Smoc.Streaming.Subsonic.Models;

/// <summary>
/// A streaming client implementation for Subsonic-compatible APIs.
/// </summary>
public sealed class SubsonicStreamingClient : IStreamingClient {
  private readonly HttpClient _httpClient;
  private readonly ICacheService _songCacheService;
  private readonly ICacheService _albumArtCacheService;
  private readonly string _uriHost;
  private readonly string _uriScheme;
  private readonly int _uriPort;
  private readonly string _username;
  private readonly string _password;
  private readonly bool _useToken;

  private SubsonicStreamingClient(string uriScheme, string uriHost, int uriPort, string username, string password, bool useToken, ICacheService songCacheService, ICacheService albumArtCacheService) {
    _uriHost = uriHost;
    _uriScheme = uriScheme;
    _uriPort = uriPort;
    _username = username;
    _password = password;
    _useToken = useToken;
    _songCacheService = songCacheService;
    _albumArtCacheService = albumArtCacheService;
    _httpClient = new HttpClient();
  }

  /// <summary>
  /// Creates a new instance of the <see cref="SubsonicStreamingClient"/> with explicit credentials.
  /// </summary>
  /// <param name="songCacheService">The song cache service.</param>
  /// <param name="albumArtCacheService">The album art cache service.</param>
  /// <param name="credentials">The connection and authentication credentials.</param>
  /// <returns>A new <see cref="SubsonicStreamingClient"/>.</returns>
  public static SubsonicStreamingClient Create(ICacheService songCacheService, ICacheService albumArtCacheService, SubsonicCredentials credentials) {
    ArgumentNullException.ThrowIfNull(credentials);
    if (string.IsNullOrWhiteSpace(credentials.Host)) {
      throw new InvalidOperationException("Subsonic Server Host not configured");
    }
    if (string.IsNullOrWhiteSpace(credentials.Username)) {
      throw new InvalidOperationException("Subsonic Username not configured");
    }
    if (string.IsNullOrEmpty(credentials.Password)) {
      throw new InvalidOperationException("Subsonic Password not configured");
    }

    return new SubsonicStreamingClient(
      credentials.Scheme,
      credentials.Host,
      credentials.Port,
      credentials.Username,
      credentials.Password,
      credentials.UseToken,
      songCacheService,
      albumArtCacheService
    );
  }

  /// <summary>
  /// Internal constructor for testing purposes.
  /// </summary>
  internal static SubsonicStreamingClient CreateForTesting(string baseUrl, string username, string password, bool useToken, ICacheService? songCacheService = null, ICacheService? albumArtCacheService = null) {
    return new SubsonicStreamingClient("http", baseUrl, 80, username, password, useToken, songCacheService ?? new NoCachingCacheService(), albumArtCacheService ?? new NoCachingCacheService());
  }

  /// <inheritdoc/>
  public async Task<List<Artist>> SearchArtistsAsync(string query, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("search3.view", new Dictionary<string, string> { { "query", query }, { "artistCount", "20" } }, cancellationToken);
    if (response.TryGetProperty("searchResult3", out var searchResult)) {
      var result = searchResult.Deserialize<SubsonicModels.SearchResult3>();
      return result?.Artists?.Select(SubsonicMapper.MapArtist).ToList() ?? [];
    }
    return [];
  }

  /// <inheritdoc/>
  public async Task<List<Song>> SearchSongsAsync(string query, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("search3.view", new Dictionary<string, string> { { "query", query }, { "songCount", "50" } }, cancellationToken);
    if (response.TryGetProperty("searchResult3", out var searchResult)) {
      var result = searchResult.Deserialize<SubsonicModels.SearchResult3>();
      return result?.Songs?.Select(s => SubsonicMapper.MapSong(s, id => BuildUrl("getCoverArt.view", new() { { "id", id } }))).ToList() ?? [];
    }
    return [];
  }

  /// <inheritdoc/>
  public async Task<Song> GetSongAsync(string songId, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getSong.view", new Dictionary<string, string> { { "id", songId } }, cancellationToken);
    if (response.TryGetProperty("song", out var songElement)) {
      var dto = songElement.Deserialize<SubsonicModels.Song>();
      return SubsonicMapper.MapSong(dto!, id => BuildUrl("getCoverArt.view", new() { { "id", id } }));
    }
    throw new Exception("Song not found in response");
  }

  /// <inheritdoc/>
  public async Task<Artist> GetArtistAsync(string artistId, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getArtist.view", new Dictionary<string, string> { { "id", artistId } }, cancellationToken);
    if (response.TryGetProperty("artist", out var artistElement)) {
      var result = artistElement.Deserialize<SubsonicModels.ArtistWithAlbums>();
      return SubsonicMapper.MapArtist(result!);
    }
    throw new Exception("Artist not found in response");
  }

  /// <inheritdoc/>
  public async Task<List<Album>> GetAlbumsByArtistAsync(Artist artist, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getArtist.view", new Dictionary<string, string> { { "id", artist.Id } }, cancellationToken);
    if (response.TryGetProperty("artist", out var artistElement)) {
      var result = artistElement.Deserialize<SubsonicModels.ArtistWithAlbums>();
      return result?.Albums?.Select(a => SubsonicMapper.MapAlbum(a, artist, id => BuildUrl("getCoverArt.view", new() { { "id", id } }))).ToList() ?? [];
    }
    return [];
  }

  /// <inheritdoc/>
  public async Task<List<Song>> GetSongsByAlbumAsync(Album album, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getAlbum.view", new Dictionary<string, string> { { "id", album.Id } }, cancellationToken);
    if (response.TryGetProperty("album", out var albumElement)) {
      var result = albumElement.Deserialize<SubsonicModels.AlbumWithSongs>();
      return result?.Songs?.Select(s => SubsonicMapper.MapSong(s, album)).ToList() ?? [];
    }
    return [];
  }

  /// <inheritdoc/>
  public async Task<SongStream> GetSongStreamAsync(string songId, CancellationToken cancellationToken = default) {
    var stream = await _songCacheService.GetOrAddAsync(
      string.Concat("subsonic-", songId),
      async ct => {
        var url = BuildUrl("stream.view", new Dictionary<string, string> { { "id", songId } });
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        using var contentStream = await response.Content.ReadAsStreamAsync(ct) ?? throw new InvalidOperationException("Failed to get song stream");
        var memoryStream = new MemoryStream();
        await contentStream.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;
        return memoryStream;
      },
      cancellationToken);
    return new SongStream(songId, "mp3", stream);
  }

  /// <inheritdoc/>
  public async Task<List<Song>> GetLikedSongsAsync(CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getStarred.view", null, cancellationToken);
    if (response.TryGetProperty("starred", out var starredElement)) {
      var result = starredElement.Deserialize<SubsonicModels.SearchResult3>();
      return result?.Songs?.Select(s => SubsonicMapper.MapSong(s, id => BuildUrl("getCoverArt.view", new() { { "id", id } }))).ToList() ?? [];
    }
    return [];
  }

  /// <inheritdoc/>
  public async Task<List<Playlist>> SearchPlaylistsAsync(string query, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getPlaylists.view", null, cancellationToken);
    if (response.TryGetProperty("playlists", out var playlistsElement)) {
      if (playlistsElement.TryGetProperty("playlist", out var playlistArray)) {
        var result = playlistArray.Deserialize<List<SubsonicModels.Playlist>>();
        return result?.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                     .Select(SubsonicMapper.MapPlaylist).ToList() ?? [];
      }
    }
    return [];
  }

  /// <inheritdoc/>
  public async Task<List<Song>> GetPlaylistSongsAsync(Playlist playlist, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getPlaylist.view", new Dictionary<string, string> { { "id", playlist.Id } }, cancellationToken);
    if (response.TryGetProperty("playlist", out var playlistElement)) {
      var result = playlistElement.Deserialize<SubsonicModels.PlaylistWithSongs>();
      return result?.Songs?.Select(s => SubsonicMapper.MapSong(s, id => BuildUrl("getCoverArt.view", new() { { "id", id } }))).ToList() ?? [];
    }
    return [];
  }

  /// <inheritdoc/>
  public Task<List<Song>> GetPlaylistSongsFromUrlAsync(string url, CancellationToken cancellationToken = default) {
    return Task.FromResult(new List<Song>());
  }

  /// <inheritdoc/>
  public async Task AddToListenHistory(Song song, CancellationToken cancellationToken = default) {
    await GetResponseElementAsync("scrobble.view", new Dictionary<string, string> { { "id", song.Id }, { "submission", "true" } }, cancellationToken);
  }

  /// <inheritdoc/>
  public async Task<Image<Rgba32>> GetAlbumArtAsync(Album album, Func<IEnumerable<AlbumCover>, AlbumCover>? coverSelector = null, CancellationToken cancellationToken = default) {
    using var albumArt = await _albumArtCacheService.GetOrAddAsync(
      string.Concat("subsonic-", album.Id),
      async ct => {
        var cover = (coverSelector?.Invoke(album.Covers) ?? album.Covers.FirstOrDefault()) ?? throw new Exception("No album cover available");
        var response = await _httpClient.GetAsync((string?)cover.Url, ct);
        return await response.Content.ReadAsStreamAsync(ct);
      },
      cancellationToken);
    return await Image.LoadAsync<Rgba32>(albumArt, cancellationToken);
  }

  private string BuildUrl(string method, Dictionary<string, string>? parameters = null) {
    return SubsonicApi.BuildUri(_uriScheme, _uriHost, _uriPort, method, _username, _password, _useToken, parameters).ToString();
  }

  private async Task<JsonElement> GetResponseElementAsync(string method, Dictionary<string, string>? parameters = null, CancellationToken cancellationToken = default) {
    var url = BuildUrl(method, parameters);
    var json = await _httpClient.GetStringAsync(url, cancellationToken);

    using var doc = JsonDocument.Parse(json);
    return SubsonicApi.ValidateResponse(doc.RootElement).Clone();
  }
}
