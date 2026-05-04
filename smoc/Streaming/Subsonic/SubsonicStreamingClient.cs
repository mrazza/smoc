using System.Net.Http.Json;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Smoc.Configuration;
using Smoc.Services;
using Smoc.Services.Caching;
using Smoc.Streaming.Subsonic.Models;

namespace Smoc.Streaming.Subsonic;

/// <summary>
/// A streaming client implementation for Subsonic-compatible APIs.
/// </summary>
public class SubsonicStreamingClient : IStreamingClient {
  private readonly HttpClient _httpClient;
  private readonly ICacheService _songCache;
  private readonly ICacheService _artCache;
  private readonly string _baseUrl;
  private readonly string _username;
  private readonly string _password;
  private readonly bool _useToken;

  private SubsonicStreamingClient(string baseUrl, string username, string password, bool useToken, ICacheService songCache, ICacheService artCache) {
    _baseUrl = baseUrl.TrimEnd('/');
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
      SubsonicConfig.ServerUrl ?? throw new InvalidOperationException("Subsonic Server URL not configured"),
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
    return new SubsonicStreamingClient(baseUrl, username, password, useToken, new NoCachingCacheService(), new NoCachingCacheService());
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

    return $"{_baseUrl}/rest/{method}?{string.Join("&", query)}";
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

  /// <inheritdoc/>
  public async Task<List<Smoc.Streaming.Artist>> SearchArtistsAsync(string query, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("search3.view", new Dictionary<string, string> { { "query", query }, { "artistCount", "20" } }, cancellationToken);
    if (response.TryGetProperty("searchResult3", out var searchResult)) {
      var result = searchResult.Deserialize<SearchResult3>();
      return result?.Artists?.Select(a => new Smoc.Streaming.Artist(a.Id, a.Name)).ToList() ?? new List<Smoc.Streaming.Artist>();
    }
    return new List<Smoc.Streaming.Artist>();
  }

  /// <inheritdoc/>
  public async Task<List<Smoc.Streaming.Song>> SearchSongsAsync(string query, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("search3.view", new Dictionary<string, string> { { "query", query }, { "songCount", "50" } }, cancellationToken);
    if (response.TryGetProperty("searchResult3", out var searchResult)) {
      var result = searchResult.Deserialize<SearchResult3>();
      return result?.Songs?.Select(s => MapSong(s)).ToList() ?? new List<Smoc.Streaming.Song>();
    }
    return new List<Smoc.Streaming.Song>();
  }

  /// <inheritdoc/>
  public async Task<Smoc.Streaming.Song> GetSongAsync(string songId, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getSong.view", new Dictionary<string, string> { { "id", songId } }, cancellationToken);
    if (response.TryGetProperty("song", out var songElement)) {
      var dto = songElement.Deserialize<Smoc.Streaming.Subsonic.Models.Song>();
      return MapSong(dto!);
    }
    throw new Exception("Song not found in response");
  }

  /// <inheritdoc/>
  public async Task<Smoc.Streaming.Artist> GetArtistAsync(string artistId, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getArtist.view", new Dictionary<string, string> { { "id", artistId } }, cancellationToken);
    if (response.TryGetProperty("artist", out var artistElement)) {
      var result = artistElement.Deserialize<ArtistWithAlbums>();
      return new Smoc.Streaming.Artist(result!.Id, result.Name);
    }
    throw new Exception("Artist not found in response");
  }

  /// <inheritdoc/>
  public async Task<List<Smoc.Streaming.Album>> GetAlbumsByArtistAsync(Smoc.Streaming.Artist artist, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getArtist.view", new Dictionary<string, string> { { "id", artist.Id } }, cancellationToken);
    if (response.TryGetProperty("artist", out var artistElement)) {
      var result = artistElement.Deserialize<ArtistWithAlbums>();
      return result?.Albums?.Select(a => MapAlbum(a, artist)).ToList() ?? new List<Smoc.Streaming.Album>();
    }
    return new List<Smoc.Streaming.Album>();
  }

  /// <inheritdoc/>
  public async Task<List<Smoc.Streaming.Song>> GetSongsByAlbumAsync(Smoc.Streaming.Album album, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getAlbum.view", new Dictionary<string, string> { { "id", album.Id } }, cancellationToken);
    if (response.TryGetProperty("album", out var albumElement)) {
      var result = albumElement.Deserialize<AlbumWithSongs>();
      return result?.Songs?.Select(s => MapSong(s, album)).ToList() ?? new List<Smoc.Streaming.Song>();
    }
    return new List<Smoc.Streaming.Song>();
  }

  /// <inheritdoc/>
  public async Task<SongStream> GetSongStreamAsync(string songId, CancellationToken cancellationToken = default) {
    var url = BuildUrl("stream.view", new Dictionary<string, string> { { "id", songId } });
    var stream = await _httpClient.GetStreamAsync(url, cancellationToken);
    return new SongStream(songId, "mp3", stream);
  }

  /// <inheritdoc/>
  public async Task<List<Smoc.Streaming.Song>> GetLikedSongsAsync(CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getStarred.view", null, cancellationToken);
    if (response.TryGetProperty("starred", out var starredElement)) {
        var result = starredElement.Deserialize<SearchResult3>();
        return result?.Songs?.Select(s => MapSong(s)).ToList() ?? new List<Smoc.Streaming.Song>();
    }
    return new List<Smoc.Streaming.Song>();
  }

  /// <inheritdoc/>
  public async Task<List<Smoc.Streaming.Playlist>> SearchPlaylistsAsync(string query, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getPlaylists.view", null, cancellationToken);
    if (response.TryGetProperty("playlists", out var playlistsElement)) {
        if (playlistsElement.TryGetProperty("playlist", out var playlistArray)) {
            var result = playlistArray.Deserialize<List<Smoc.Streaming.Subsonic.Models.Playlist>>();
            return result?.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                         .Select(p => new Smoc.Streaming.Playlist(p.Id, p.Name)).ToList() ?? new List<Smoc.Streaming.Playlist>();
        }
    }
    return new List<Smoc.Streaming.Playlist>();
  }

  /// <inheritdoc/>
  public async Task<List<Smoc.Streaming.Song>> GetPlaylistSongsAsync(Smoc.Streaming.Playlist playlist, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getPlaylist.view", new Dictionary<string, string> { { "id", playlist.Id } }, cancellationToken);
    if (response.TryGetProperty("playlist", out var playlistElement)) {
        var result = playlistElement.Deserialize<PlaylistWithSongs>();
        return result?.Songs?.Select(s => MapSong(s)).ToList() ?? new List<Smoc.Streaming.Song>();
    }
    return new List<Smoc.Streaming.Song>();
  }

  /// <inheritdoc/>
  public Task<List<Smoc.Streaming.Song>> GetPlaylistSongsFromUrlAsync(string url, CancellationToken cancellationToken = default) {
    return Task.FromResult(new List<Smoc.Streaming.Song>());
  }

  /// <inheritdoc/>
  public async Task AddToListenHistory(Smoc.Streaming.Song song, CancellationToken cancellationToken = default) {
    await GetResponseElementAsync("scrobble.view", new Dictionary<string, string> { { "id", song.Id }, { "submission", "true" } }, cancellationToken);
  }

  /// <inheritdoc/>
  public async Task<Image<Rgba32>> GetAlbumArtAsync(Smoc.Streaming.Album album, Func<IEnumerable<AlbumCover>, AlbumCover>? coverSelector = null, CancellationToken cancellationToken = default) {
    var cover = coverSelector?.Invoke(album.Covers) ?? album.Covers.FirstOrDefault();
    if (cover == null) throw new Exception("No album cover available");

    var url = cover.Url;
    var response = await _httpClient.GetAsync(url, cancellationToken);
    var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
    return await Image.LoadAsync<Rgba32>(stream, cancellationToken);
  }

  internal Smoc.Streaming.Song MapSong(Smoc.Streaming.Subsonic.Models.Song dto) {
    var artist = new Smoc.Streaming.Artist(dto.ArtistId ?? "", dto.ArtistName ?? "Unknown Artist");
    var album = new Smoc.Streaming.Album(
      dto.AlbumId ?? "",
      artist,
      dto.AlbumName ?? "Unknown Album",
      dto.CoverArt != null ? new[] { new AlbumCover(BuildUrl("getCoverArt.view", new Dictionary<string, string> { { "id", dto.CoverArt } }), 0, 0) } : Enumerable.Empty<AlbumCover>()
    );
    return MapSong(dto, album);
  }

  internal Smoc.Streaming.Song MapSong(Smoc.Streaming.Subsonic.Models.Song dto, Smoc.Streaming.Album album) {
    return new Smoc.Streaming.Song(dto.Id, album, dto.Title, TimeSpan.FromSeconds(dto.Duration ?? 0), dto.Track);
  }

  internal Smoc.Streaming.Album MapAlbum(Smoc.Streaming.Subsonic.Models.Album dto, Smoc.Streaming.Artist artist) {
    return new Smoc.Streaming.Album(
      dto.Id,
      artist,
      dto.Name,
      dto.CoverArt != null ? new[] { new AlbumCover(BuildUrl("getCoverArt.view", new Dictionary<string, string> { { "id", dto.CoverArt } }), 0, 0) } : Enumerable.Empty<AlbumCover>()
    );
  }
}
