using System.Net.Http.Json;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Smoc.Configuration;
using Smoc.Services;
using Smoc.Services.Caching;

namespace Smoc.Streaming.Subsonic;

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

  public async Task<List<Artist>> SearchArtistsAsync(string query, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("search3.view", new Dictionary<string, string> { { "query", query }, { "artistCount", "20" } }, cancellationToken);
    if (response.TryGetProperty("searchResult3", out var searchResult)) {
      var result = searchResult.Deserialize<SearchResult3>();
      return result?.Artists?.Select(a => new Artist(a.Id, a.Name)).ToList() ?? new List<Artist>();
    }
    return new List<Artist>();
  }

  public async Task<List<Song>> SearchSongsAsync(string query, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("search3.view", new Dictionary<string, string> { { "query", query }, { "songCount", "50" } }, cancellationToken);
    if (response.TryGetProperty("searchResult3", out var searchResult)) {
      var result = searchResult.Deserialize<SearchResult3>();
      return result?.Songs?.Select(s => MapSong(s)).ToList() ?? new List<Song>();
    }
    return new List<Song>();
  }

  public async Task<Song> GetSongAsync(string songId, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getSong.view", new Dictionary<string, string> { { "id", songId } }, cancellationToken);
    if (response.TryGetProperty("song", out var songElement)) {
      var dto = songElement.Deserialize<SongDto>();
      return MapSong(dto!);
    }
    throw new Exception("Song not found in response");
  }

  public async Task<Artist> GetArtistAsync(string artistId, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getArtist.view", new Dictionary<string, string> { { "id", artistId } }, cancellationToken);
    if (response.TryGetProperty("artist", out var artistElement)) {
      var result = artistElement.Deserialize<ArtistWithAlbumsDto>();
      return new Artist(result!.Id, result.Name);
    }
    throw new Exception("Artist not found in response");
  }

  public async Task<List<Album>> GetAlbumsByArtistAsync(Artist artist, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getArtist.view", new Dictionary<string, string> { { "id", artist.Id } }, cancellationToken);
    if (response.TryGetProperty("artist", out var artistElement)) {
      var result = artistElement.Deserialize<ArtistWithAlbumsDto>();
      return result?.Albums?.Select(a => MapAlbum(a, artist)).ToList() ?? new List<Album>();
    }
    return new List<Album>();
  }

  public async Task<List<Song>> GetSongsByAlbumAsync(Album album, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getAlbum.view", new Dictionary<string, string> { { "id", album.Id } }, cancellationToken);
    if (response.TryGetProperty("album", out var albumElement)) {
      var result = albumElement.Deserialize<AlbumWithSongsDto>();
      return result?.Songs?.Select(s => MapSong(s, album)).ToList() ?? new List<Song>();
    }
    return new List<Song>();
  }

  public async Task<SongStream> GetSongStreamAsync(string songId, CancellationToken cancellationToken = default) {
    var url = BuildUrl("stream.view", new Dictionary<string, string> { { "id", songId } });
    var stream = await _httpClient.GetStreamAsync(url, cancellationToken);
    return new SongStream(songId, "mp3", stream); // Assuming mp3 for now, could be dynamic
  }

  public async Task<List<Song>> GetLikedSongsAsync(CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getStarred.view", null, cancellationToken);
    if (response.TryGetProperty("starred", out var starredElement)) {
        var result = starredElement.Deserialize<SearchResult3>();
        return result?.Songs?.Select(s => MapSong(s)).ToList() ?? new List<Song>();
    }
    return new List<Song>();
  }

  public async Task<List<Playlist>> SearchPlaylistsAsync(string query, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getPlaylists.view", null, cancellationToken);
    if (response.TryGetProperty("playlists", out var playlistsElement)) {
        if (playlistsElement.TryGetProperty("playlist", out var playlistArray)) {
            var result = playlistArray.Deserialize<List<PlaylistDto>>();
            return result?.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                         .Select(p => new Playlist(p.Id, p.Name)).ToList() ?? new List<Playlist>();
        }
    }
    return new List<Playlist>();
  }

  public async Task<List<Song>> GetPlaylistSongsAsync(Playlist playlist, CancellationToken cancellationToken = default) {
    var response = await GetResponseElementAsync("getPlaylist.view", new Dictionary<string, string> { { "id", playlist.Id } }, cancellationToken);
    if (response.TryGetProperty("playlist", out var playlistElement)) {
        var result = playlistElement.Deserialize<PlaylistWithSongsDto>();
        return result?.Songs?.Select(s => MapSong(s)).ToList() ?? new List<Song>();
    }
    return new List<Song>();
  }

  public Task<List<Song>> GetPlaylistSongsFromUrlAsync(string url, CancellationToken cancellationToken = default) {
    return Task.FromResult(new List<Song>());
  }

  public async Task AddToListenHistory(Song song, CancellationToken cancellationToken = default) {
    await GetResponseElementAsync("scrobble.view", new Dictionary<string, string> { { "id", song.Id }, { "submission", "true" } }, cancellationToken);
  }

  public async Task<Image<Rgba32>> GetAlbumArtAsync(Album album, Func<IEnumerable<AlbumCover>, AlbumCover>? coverSelector = null, CancellationToken cancellationToken = default) {
    var cover = coverSelector?.Invoke(album.Covers) ?? album.Covers.FirstOrDefault();
    if (cover == null) throw new Exception("No album cover available");

    var url = cover.Url;
    var response = await _httpClient.GetAsync(url, cancellationToken);
    var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
    return await Image.LoadAsync<Rgba32>(stream, cancellationToken);
  }

  internal Song MapSong(SongDto dto) {
    var artist = new Artist(dto.ArtistId ?? "", dto.Artist ?? "Unknown Artist");
    var album = new Album(
      dto.AlbumId ?? "",
      artist,
      dto.Album ?? "Unknown Album",
      dto.CoverArt != null ? new[] { new AlbumCover(BuildUrl("getCoverArt.view", new Dictionary<string, string> { { "id", dto.CoverArt } }), 0, 0) } : Enumerable.Empty<AlbumCover>()
    );
    return MapSong(dto, album);
  }

  internal Song MapSong(SongDto dto, Album album) {
    return new Song(dto.Id, album, dto.Title, TimeSpan.FromSeconds(dto.Duration ?? 0), dto.Track);
  }

  internal Album MapAlbum(AlbumDto dto, Artist artist) {
    return new Album(
      dto.Id,
      artist,
      dto.Name,
      dto.CoverArt != null ? new[] { new AlbumCover(BuildUrl("getCoverArt.view", new Dictionary<string, string> { { "id", dto.CoverArt } }), 0, 0) } : Enumerable.Empty<AlbumCover>()
    );
  }
}
