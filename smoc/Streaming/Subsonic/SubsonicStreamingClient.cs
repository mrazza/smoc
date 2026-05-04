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
public class SubsonicStreamingClient : IStreamingClient {
  private readonly HttpClient _httpClient;
  private readonly ICacheService _songCache;
  private readonly ICacheService _artCache;
  private readonly string _uriHost;
  private readonly string _uriScheme;
  private readonly int _uriPort;
  private readonly string _username;
  private readonly string _password;
  private readonly bool _useToken;

  private SubsonicStreamingClient(string uriScheme, string uriHost, int uriPort, string username, string password, bool useToken, ICacheService songCache, ICacheService artCache) {
    _uriHost = uriHost;
    _uriScheme = uriScheme;
    _uriPort = uriPort;
    _username = username;
    _password = password;
    _useToken = useToken;
    _songCache = songCache;
    _artCache = artCache;
    _httpClient = new HttpClient();
  }

  /// <summary>
  /// Creates a new instance of the <see cref="SubsonicStreamingClient"/>.
  /// </summary>
  /// <param name="songCache">The song cache service.</param>
  /// <param name="artCache">The album art cache service.</param>
  /// <returns>A new <see cref="SubsonicStreamingClient"/>.</returns>
  public static SubsonicStreamingClient Create(ICacheService songCache, ICacheService artCache) {
    return new SubsonicStreamingClient(
      SubsonicConfig.ServerScheme,
      SubsonicConfig.ServerHost ?? throw new InvalidOperationException("Subsonic Server Host not configured"),
      SubsonicConfig.ServerPort,
      SubsonicConfig.Username ?? throw new InvalidOperationException("Subsonic Username not configured"),
      SubsonicConfig.Password ?? throw new InvalidOperationException("Subsonic Password not configured"),
      SubsonicConfig.UseToken,
      songCache,
      artCache
    );
  }

  /// <summary>
  /// Internal constructor for testing purposes.
  /// </summary>
  internal static SubsonicStreamingClient CreateForTesting(string baseUrl, string username, string password, bool useToken) {
    return new SubsonicStreamingClient("http", baseUrl, 80, username, password, useToken, new NoCachingCacheService(), new NoCachingCacheService());
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
    var url = BuildUrl("stream.view", new Dictionary<string, string> { { "id", songId } });
    using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
    if (stream is null) throw new Exception("Failed to get song stream");
    var memoryStream = new MemoryStream();
    await stream.CopyToAsync(memoryStream, cancellationToken);
    memoryStream.Position = 0;
    return new SongStream(songId, "mp3", memoryStream);
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
    var cover = (coverSelector?.Invoke(album.Covers) ?? album.Covers.FirstOrDefault()) ?? throw new Exception("No album cover available");
    var url = cover.Url;
    var response = await _httpClient.GetAsync(url, cancellationToken);
    var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
    return await Image.LoadAsync<Rgba32>(stream, cancellationToken);
  }

  private string BuildUrl(string method, Dictionary<string, string>? parameters = null) {
    var query = new List<string> {
      $"u={_username}",
      "v=1.16.1",
      "c=smoc",
      "f=json"
    };

    if (_useToken) {
      var (token, salt) = SubsonicAuthentication.GenerateToken(_password);
      query.Add($"t={token}");
      query.Add($"s={salt}");
    } else {
      query.Add($"p={_password}");
    }

    if (parameters != null) {
      foreach (var param in parameters) {
        query.Add($"{param.Key}={Uri.EscapeDataString(param.Value)}");
      }
    }

    return new UriBuilder(_uriScheme, _uriHost, _uriPort, $"rest/{method}") {
      Query = string.Join("&", query)
    }.ToString();
  }

  private async Task<JsonElement> GetResponseElementAsync(string method, Dictionary<string, string>? parameters = null, CancellationToken cancellationToken = default) {
    var url = BuildUrl(method, parameters);
    var json = await _httpClient.GetStringAsync(url, cancellationToken);

    using var doc = JsonDocument.Parse(json);
    if (!doc.RootElement.TryGetProperty("subsonic-response", out var responseElement)) {
      throw new Exception("Missing subsonic-response");
    }

    if (responseElement.TryGetProperty("status", out var status) && status.GetString() == "failed") {
      if (responseElement.TryGetProperty("error", out var error)) {
        var code = error.TryGetProperty("code", out var c) ? c.GetInt32() : 0;
        var message = error.TryGetProperty("message", out var m) ? m.GetString() : "Unknown Subsonic error";
        throw new Exception($"Subsonic API error {code}: {message}");
      }
    }

    return responseElement.Clone();
  }
}