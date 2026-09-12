using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Smoc.Configuration;
using Smoc.Services;
using Smoc.Services.Caching;
using Smoc.Streaming.Tidal.Models;
using Terminal.Gui.App;

namespace Smoc.Streaming.Tidal;

/// <summary>
/// Client for interacting with the Tidal music streaming service.
/// </summary>
public sealed class TidalStreamingClient : IStreamingClient, IDisposable {
  private static readonly string AuthUrl = "https://auth.tidal.com/v1/oauth2";
  private static readonly string ApiUrl = "https://api.tidal.com/v1";
  private static readonly string DefaultTokensPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".config",
    "smoc",
    "tidal_tokens.json");

  private readonly HttpClient _httpClient;
  private readonly ICacheService _songCacheService;
  private readonly ICacheService _albumArtCacheService;
  private readonly SemaphoreSlim _authSemaphore = new(1, 1);
  private bool _isDisposed;

  /// <summary>
  /// Initializes a new instance of the <see cref="TidalStreamingClient"/> class.
  /// </summary>
  /// <param name="httpClient">The HTTP client to use for network calls.</param>
  /// <param name="songCacheService">Optional cache service for songs.</param>
  /// <param name="albumArtCacheService">Optional cache service for album art.</param>
  internal TidalStreamingClient(HttpClient httpClient, ICacheService? songCacheService = null, ICacheService? albumArtCacheService = null) {
    _httpClient = httpClient;
    _songCacheService = songCacheService ?? new NoCachingCacheService();
    _albumArtCacheService = albumArtCacheService ?? new NoCachingCacheService();
  }

  /// <summary>
  /// Creates a new instance of <see cref="TidalStreamingClient"/>.
  /// </summary>
  /// <param name="songCacheService">Optional cache service for songs.</param>
  /// <param name="albumArtCacheService">Optional cache service for album art.</param>
  /// <returns>A new <see cref="TidalStreamingClient"/> instance.</returns>
  public static TidalStreamingClient Create(ICacheService? songCacheService = null, ICacheService? albumArtCacheService = null) {
    return new TidalStreamingClient(new HttpClient(), songCacheService, albumArtCacheService);
  }

  private static bool IsTokenExpired() {
    if (string.IsNullOrEmpty(TidalConfig.Defaults.AccessToken)) {
      return true;
    }

    if (TidalConfig.Defaults.TokenExpiry.HasValue) {
      var nowEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
      return TidalConfig.Defaults.TokenExpiry.Value < (nowEpoch + 300);
    }

    return false;
  }

  private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken = default) {
    if (!IsTokenExpired()) {
      return;
    }

    await _authSemaphore.WaitAsync(cancellationToken);
    try {
      if (!IsTokenExpired()) {
        return;
      }

      if (!string.IsNullOrEmpty(TidalConfig.Defaults.RefreshToken)) {
        await RefreshTokenAsync(cancellationToken);
      } else {
        Logging.Warning("Tidal access token is missing or expired, and no refresh token is configured.");
      }
    } finally {
      _authSemaphore.Release();
    }
  }

  internal async Task RefreshTokenAsync(CancellationToken cancellationToken = default) {
    if (string.IsNullOrEmpty(TidalConfig.Defaults.RefreshToken) || string.IsNullOrEmpty(TidalConfig.Defaults.ClientId)) {
      Logging.Warning("Tidal token refresh skipped: RefreshToken or ClientId is not configured.");
      return;
    }

    Logging.Information("Refreshing Tidal access token...");
    var refreshRequest = new Dictionary<string, string> {
      { "client_id", TidalConfig.Defaults.ClientId },
      { "refresh_token", TidalConfig.Defaults.RefreshToken },
      { "grant_type", "refresh_token" }
    };

    using var response = await _httpClient.PostAsync($"{AuthUrl}/token", new FormUrlEncodedContent(refreshRequest), cancellationToken);
    if (response.IsSuccessStatusCode) {
      var tokenData = await response.Content.ReadFromJsonAsync<TidalTokenResponse>(cancellationToken: cancellationToken);
      if (tokenData != null) {
        TidalConfig.Defaults.AccessToken = tokenData.AccessToken;
        TidalConfig.Defaults.TokenExpiry = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + tokenData.ExpiresIn;
        if (!string.IsNullOrEmpty(tokenData.RefreshToken)) {
          TidalConfig.Defaults.RefreshToken = tokenData.RefreshToken;
        }

        SaveTokens(TidalConfig.Defaults.AccessToken, TidalConfig.Defaults.RefreshToken, TidalConfig.Defaults.TokenExpiry, TidalConfig.Defaults.ClientId);
        Logging.Information("Tidal access token refreshed successfully.");
        return;
      }
    }

    Logging.Warning($"Tidal token refresh failed with status: {response.StatusCode}");
  }

  /// <summary>
  /// Authenticates interactively with Tidal using OAuth device authorization flow.
  /// </summary>
  /// <param name="clientId">The client ID to use, or null to prompt/use default.</param>
  /// <param name="tokensPath">The path where tokens should be saved.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  public static async Task AuthenticateInteractiveAsync(string? clientId, string tokensPath, CancellationToken cancellationToken = default) {
    using var httpClient = new HttpClient();
    var activeClientId = clientId;

    if (string.IsNullOrWhiteSpace(activeClientId)) {
      Console.Write("Enter Tidal Client ID [press Enter for default]: ");
      var input = Console.ReadLine()?.Trim();
      activeClientId = string.IsNullOrEmpty(input) ? "zU4XHVVkc2tDPo4t" : input;
    }

    Console.WriteLine("Requesting Tidal device authorization...");
    var authRequest = new Dictionary<string, string> {
      { "client_id", activeClientId },
      { "scope", "user" }
    };

    using var authResponse = await httpClient.PostAsync($"{AuthUrl}/device/authorization", new FormUrlEncodedContent(authRequest), cancellationToken);
    authResponse.EnsureSuccessStatusCode();

    var authData = await authResponse.Content.ReadFromJsonAsync<TidalDeviceAuthResponse>(cancellationToken: cancellationToken)
      ?? throw new InvalidOperationException("Failed to parse device authorization response.");

    Console.WriteLine();
    Console.WriteLine($"Please visit: {authData.VerificationUriComplete}");
    Console.WriteLine($"Or navigate to {authData.VerificationUri} and enter code: {authData.UserCode}");
    Console.WriteLine("Waiting for authorization...");

    var tokenRequest = new Dictionary<string, string> {
      { "client_id", activeClientId },
      { "device_code", authData.DeviceCode },
      { "grant_type", "urn:ietf:params:oauth:grant-type:device_code" }
    };

    var intervalSeconds = Math.Max(authData.Interval, 1);
    while (!cancellationToken.IsCancellationRequested) {
      await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);

      using var tokenResponse = await httpClient.PostAsync($"{AuthUrl}/token", new FormUrlEncodedContent(tokenRequest), cancellationToken);
      if (tokenResponse.IsSuccessStatusCode) {
        var tokenData = await tokenResponse.Content.ReadFromJsonAsync<TidalTokenResponse>(cancellationToken: cancellationToken);
        if (tokenData != null) {
          var tokenExpiry = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + tokenData.ExpiresIn;
          TidalConfig.Defaults.AccessToken = tokenData.AccessToken;
          TidalConfig.Defaults.RefreshToken = tokenData.RefreshToken;
          TidalConfig.Defaults.TokenExpiry = tokenExpiry;
          TidalConfig.Defaults.ClientId = activeClientId;

          SaveTokens(tokenData.AccessToken, tokenData.RefreshToken, tokenExpiry, activeClientId, tokensPath);
          Console.WriteLine();
          Console.WriteLine($"Tidal authentication successful! Tokens saved to {tokensPath}");
          return;
        }
      }

      var errorContent = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
      if (!errorContent.Contains("authorization_pending", StringComparison.OrdinalIgnoreCase)) {
        throw new InvalidOperationException($"Tidal authentication failed: {errorContent}");
      }
    }
  }

  private static void SaveTokens(string? accessToken, string? refreshToken, long? tokenExpiry, string? clientId, string? path = null) {
    try {
      var filePath = path ?? DefaultTokensPath;
      var dir = Path.GetDirectoryName(filePath);
      if (!string.IsNullOrEmpty(dir)) {
        Directory.CreateDirectory(dir);
      }

      var tokens = new TidalTokens(accessToken, refreshToken, tokenExpiry, clientId);
      File.WriteAllText(filePath, JsonSerializer.Serialize(tokens, new JsonSerializerOptions { WriteIndented = true }));
    } catch (Exception ex) {
      Logging.Warning($"Failed to save Tidal tokens: {ex.Message}");
    }
  }

  private async Task<HttpResponseMessage> SendRequestAsync(string endpoint, Dictionary<string, string>? parameters, CancellationToken cancellationToken) {
    var url = $"{ApiUrl}/{endpoint.TrimStart('/')}";
    var queryParams = parameters != null ? new Dictionary<string, string>(parameters) : new Dictionary<string, string>();
    if (!string.IsNullOrEmpty(TidalConfig.Defaults.CountryCode)) {
      queryParams["countryCode"] = TidalConfig.Defaults.CountryCode;
    }

    var queryString = string.Join("&", queryParams.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
    var fullUrl = string.IsNullOrEmpty(queryString) ? url : $"{url}?{queryString}";

    var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);
    if (!string.IsNullOrEmpty(TidalConfig.Defaults.AccessToken)) {
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TidalConfig.Defaults.AccessToken);
    }

    return await _httpClient.SendAsync(request, cancellationToken);
  }

  private async Task<T> GetAsync<T>(string endpoint, Dictionary<string, string>? parameters = null, CancellationToken cancellationToken = default) {
    await EnsureAuthenticatedAsync(cancellationToken);

    var response = await SendRequestAsync(endpoint, parameters, cancellationToken);
    if (response.StatusCode == HttpStatusCode.Unauthorized) {
      response.Dispose();
      await _authSemaphore.WaitAsync(cancellationToken);
      try {
        await RefreshTokenAsync(cancellationToken);
      } finally {
        _authSemaphore.Release();
      }

      response = await SendRequestAsync(endpoint, parameters, cancellationToken);
    }

    using (response) {
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
        ?? throw new InvalidOperationException("API returned null.");
    }
  }

  /// <inheritdoc />
  public async Task<List<Artist>> SearchArtistsAsync(string query, CancellationToken cancellationToken = default) {
    var response = await GetAsync<TidalSearchContainer>(
      "search",
      new Dictionary<string, string> { { "query", query }, { "types", "ARTISTS" }, { "limit", "20" } },
      cancellationToken);

    return response.Artists?.Items.Select(a => new Artist(a.Id.ToString(), a.Name)).ToList() ?? [];
  }

  /// <inheritdoc />
  public async Task<List<Song>> SearchSongsAsync(string query, CancellationToken cancellationToken = default) {
    var response = await GetAsync<TidalSearchContainer>(
      "search",
      new Dictionary<string, string> { { "query", query }, { "types", "TRACKS" }, { "limit", "20" } },
      cancellationToken);

    return response.Tracks?.Items.Select(MapTrackToSong).ToList() ?? [];
  }

  /// <inheritdoc />
  public async Task<Song> GetSongAsync(string songId, CancellationToken cancellationToken = default) {
    var track = await GetAsync<TidalTrack>($"/tracks/{songId}", null, cancellationToken);
    return MapTrackToSong(track);
  }

  /// <inheritdoc />
  public async Task<Artist> GetArtistAsync(string artistId, CancellationToken cancellationToken = default) {
    var artist = await GetAsync<TidalArtist>($"/artists/{artistId}", null, cancellationToken);
    return new Artist(artist.Id.ToString(), artist.Name);
  }

  /// <inheritdoc />
  public async Task<List<Album>> GetAlbumsByArtistAsync(Artist artist, CancellationToken cancellationToken = default) {
    var response = await GetAsync<TidalSearchResponse<TidalAlbum>>(
      $"/artists/{artist.Id}/albums",
      new Dictionary<string, string> { { "limit", "50" } },
      cancellationToken);

    return response.Items.Select(a => MapAlbumToAlbum(a, artist)).ToList();
  }

  /// <inheritdoc />
  public async Task<List<Song>> GetSongsByAlbumAsync(Album album, CancellationToken cancellationToken = default) {
    var response = await GetAsync<TidalSearchResponse<TidalTrack>>(
      $"/albums/{album.Id}/tracks",
      new Dictionary<string, string> { { "limit", "50" } },
      cancellationToken);

    return response.Items.Select(MapTrackToSong).ToList();
  }

  /// <inheritdoc />
  public async Task<SongStream> GetSongStreamAsync(string songId, CancellationToken cancellationToken = default) {
    var playbackInfo = await GetAsync<TidalPlaybackInfo>(
      $"/tracks/{songId}/playbackinfo",
      new Dictionary<string, string> {
        { "audioquality", TidalConfig.Defaults.Quality },
        { "playbackmode", "STREAM" },
        { "assetpresentation", "FULL" }
      },
      cancellationToken);

    if (playbackInfo.ManifestMimeType != null &&
        !playbackInfo.ManifestMimeType.Contains("bts", StringComparison.OrdinalIgnoreCase) &&
        !playbackInfo.ManifestMimeType.Contains("json", StringComparison.OrdinalIgnoreCase)) {
      throw new InvalidOperationException($"Unsupported manifest MIME type: {playbackInfo.ManifestMimeType}");
    }

    byte[] manifestBytes;
    try {
      manifestBytes = Convert.FromBase64String(playbackInfo.Manifest);
    } catch (FormatException ex) {
      throw new InvalidOperationException("Failed to decode base64 manifest from Tidal playback info.", ex);
    }

    var manifestJson = Encoding.UTF8.GetString(manifestBytes);
    TidalManifest? manifest;
    try {
      manifest = JsonSerializer.Deserialize<TidalManifest>(manifestJson);
    } catch (JsonException ex) {
      throw new InvalidOperationException("Failed to deserialize Tidal manifest JSON.", ex);
    }

    if (manifest == null || manifest.Urls == null || manifest.Urls.Count == 0) {
      throw new InvalidOperationException("No stream URLs found in manifest.");
    }

    var streamUrl = manifest.Urls[0];
    var stream = await _songCacheService.GetOrAddAsync(
      $"{songId}-{playbackInfo.AudioQuality}",
      async ct => await _httpClient.GetStreamAsync(streamUrl, ct),
      cancellationToken);

    var codec = (manifest.MimeType != null && manifest.MimeType.Contains("flac", StringComparison.OrdinalIgnoreCase)) ||
                (manifest.Codecs != null && manifest.Codecs.Contains("flac", StringComparison.OrdinalIgnoreCase))
      ? "flac"
      : "aac";

    return new SongStream(songId, codec, stream);
  }

  /// <inheritdoc />
  public async Task<List<Song>> GetLikedSongsAsync(CancellationToken cancellationToken = default) {
    return await Task.FromResult(new List<Song>());
  }

  /// <inheritdoc />
  public async Task<List<Playlist>> SearchPlaylistsAsync(string query, CancellationToken cancellationToken = default) {
    var response = await GetAsync<TidalSearchContainer>(
      "search",
      new Dictionary<string, string> { { "query", query }, { "types", "PLAYLISTS" }, { "limit", "20" } },
      cancellationToken);

    return response.Playlists?.Items.Select(p => new Playlist(p.Uuid, p.Title)).ToList() ?? [];
  }

  /// <inheritdoc />
  public async Task<List<Song>> GetPlaylistSongsAsync(Playlist playlist, CancellationToken cancellationToken = default) {
    var response = await GetAsync<TidalSearchResponse<TidalTrack>>(
      $"/playlists/{playlist.Id}/tracks",
      new Dictionary<string, string> { { "limit", "50" } },
      cancellationToken);

    return response.Items.Select(MapTrackToSong).ToList();
  }

  /// <inheritdoc />
  public async Task<List<Song>> GetPlaylistSongsFromUrlAsync(string url, CancellationToken cancellationToken = default) {
    if (!TidalUrlParser.TryParseUrl(url, out var type, out var id)) {
      return [];
    }

    return type switch {
      "track" => [await GetSongAsync(id, cancellationToken)],
      "album" => await GetSongsByAlbumAsync(new Album(id, new Artist(string.Empty, string.Empty), string.Empty, []), cancellationToken),
      "playlist" => await GetPlaylistSongsAsync(new Playlist(id, string.Empty), cancellationToken),
      _ => []
    };
  }

  /// <inheritdoc />
  public async Task AddToListenHistory(Song song, CancellationToken cancellationToken = default) {
    await Task.CompletedTask;
  }

  /// <inheritdoc />
  public async Task<Image<Rgba32>> GetAlbumArtAsync(Album album, Func<IEnumerable<AlbumCover>, AlbumCover>? coverSelector = null, CancellationToken cancellationToken = default) {
    if (!album.Covers.Any()) {
      throw new ArgumentException("Album has no covers.", nameof(album));
    }

    var cover = coverSelector?.Invoke(album.Covers) ?? album.Covers.First();

    using var albumArt = await _albumArtCacheService.GetOrAddAsync(
      string.Concat(album.Id, "-", cover.Width, "x", cover.Height),
      async ct => {
        using var albumResponse = await _httpClient.GetAsync(cover.Url, ct);
        albumResponse.EnsureSuccessStatusCode();
        var memoryStream = new MemoryStream();
        await albumResponse.Content.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;
        return memoryStream;
      },
      cancellationToken);

    return await Image.LoadAsync<Rgba32>(albumArt, cancellationToken);
  }

  internal static Song MapTrackToSong(TidalTrack track) {
    var artist = track.Artist != null
      ? new Artist(track.Artist.Id.ToString(), track.Artist.Name ?? string.Empty)
      : (track.Album?.Artist != null
          ? new Artist(track.Album.Artist.Id.ToString(), track.Album.Artist.Name ?? string.Empty)
          : new Artist(string.Empty, "Unknown Artist"));

    var album = track.Album != null
      ? MapAlbumToAlbum(track.Album, artist)
      : new Album(string.Empty, artist, "Unknown Album", []);

    return new Song(track.Id.ToString(), album, track.Title ?? "Unknown Track", TimeSpan.FromSeconds(track.Duration), track.TrackNumber);
  }

  internal static Album MapAlbumToAlbum(TidalAlbum album, Artist? artist = null) {
    var resolvedArtist = artist
      ?? (album.Artist != null ? new Artist(album.Artist.Id.ToString(), album.Artist.Name ?? string.Empty) : new Artist(string.Empty, "Unknown Artist"));

    var covers = new List<AlbumCover>();
    if (!string.IsNullOrEmpty(album.Cover)) {
      var uuid = album.Cover.Replace("-", "/");
      covers.Add(new AlbumCover($"https://resources.tidal.com/images/{uuid}/640x640.jpg", 640, 640));
      covers.Add(new AlbumCover($"https://resources.tidal.com/images/{uuid}/320x320.jpg", 320, 320));
    }

    int? releaseYear = null;
    if (!string.IsNullOrEmpty(album.ReleaseDate) && DateTime.TryParse(album.ReleaseDate, out var date)) {
      releaseYear = date.Year;
    }

    return new Album(album.Id.ToString(), resolvedArtist, album.Title ?? "Unknown Album", covers, releaseYear);
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_isDisposed) {
      return;
    }

    _httpClient.Dispose();
    _authSemaphore.Dispose();
    _isDisposed = true;
  }
}
