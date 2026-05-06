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

public sealed class TidalStreamingClient : IStreamingClient, IDisposable {
  private static readonly string AuthUrl = "https://auth.tidal.com/v1/oauth2";
  private static readonly string ApiUrl = "https://api.tidal.com/v1";
  private readonly HttpClient _httpClient;
  private readonly ICacheService _songCacheService;
  private readonly ICacheService _albumArtCacheService;
  private bool _isDisposed;

  private TidalStreamingClient(ICacheService? songCacheService = null, ICacheService? albumArtCacheService = null) {
    _httpClient = new HttpClient();
    _songCacheService = songCacheService ?? new NoCachingCacheService();
    _albumArtCacheService = albumArtCacheService ?? new NoCachingCacheService();
  }

  public static TidalStreamingClient CreateForTesting(HttpClient httpClient, ICacheService? songCacheService = null, ICacheService? albumArtCacheService = null) {
    var client = new TidalStreamingClient(songCacheService, albumArtCacheService);
    var httpClientField = typeof(TidalStreamingClient).GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    httpClientField?.SetValue(client, httpClient);
    TidalConfig.AccessToken = "test-token";
    return client;
  }

  public static TidalStreamingClient Create(ICacheService? songCacheService = null, ICacheService? albumArtCacheService = null) {
    return new TidalStreamingClient(songCacheService, albumArtCacheService);
  }

  private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken = default) {
    if (string.IsNullOrEmpty(TidalConfig.AccessToken)) {
      await AuthorizeDeviceAsync(cancellationToken);
    }
    // TODO: Implement token refresh logic if expired
  }

  private async Task AuthorizeDeviceAsync(CancellationToken cancellationToken = default) {
    if (string.IsNullOrEmpty(TidalConfig.ClientId)) {
      throw new InvalidOperationException("Tidal Client ID not configured.");
    }

    Logging.Information("Starting Tidal Device Authorization flow...");
    var authRequest = new Dictionary<string, string> {
      { "client_id", TidalConfig.ClientId },
      { "scope", "user" }
    };

    var response = await _httpClient.PostAsync($"{AuthUrl}/device/authorization", new FormUrlEncodedContent(authRequest), cancellationToken);
    var authData = await response.Content.ReadFromJsonAsync<TidalDeviceAuthResponse>(cancellationToken: cancellationToken);

    if (authData == null) throw new InvalidOperationException("Failed to get device authorization data.");

    Logging.Information($"Please visit {authData.VerificationUriComplete} or go to {authData.VerificationUri} and enter code: {authData.UserCode}");
    // In a real CLI, we would display this to the user. For now, we'll log it.
    
    // Polling logic
    var tokenRequest = new Dictionary<string, string> {
      { "client_id", TidalConfig.ClientId },
      { "device_code", authData.DeviceCode },
      { "grant_type", "urn:ietf:params:oauth:grant-type:device_code" }
    };

    while (!cancellationToken.IsCancellationRequested) {
      await Task.Delay(authData.Interval * 1000, cancellationToken);
      var tokenResponse = await _httpClient.PostAsync($"{AuthUrl}/token", new FormUrlEncodedContent(tokenRequest), cancellationToken);
      
      if (tokenResponse.IsSuccessStatusCode) {
        var tokenData = await tokenResponse.Content.ReadFromJsonAsync<TidalTokenResponse>(cancellationToken: cancellationToken);
        if (tokenData != null) {
          TidalConfig.AccessToken = tokenData.AccessToken;
          TidalConfig.RefreshToken = tokenData.RefreshToken;
          Logging.Information("Tidal authentication successful.");
          return;
        }
      }

      var errorContent = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
      if (!errorContent.Contains("authorization_pending")) {
        throw new InvalidOperationException($"Tidal auth failed: {errorContent}");
      }
    }
  }

  private async Task<T> GetAsync<T>(string endpoint, Dictionary<string, string>? parameters = null, CancellationToken cancellationToken = default) {
    await EnsureAuthenticatedAsync(cancellationToken);
    
    var url = $"{ApiUrl}/{endpoint.TrimStart('/')}";
    var queryParams = parameters ?? new Dictionary<string, string>();
    queryParams["countryCode"] = TidalConfig.CountryCode;
    
    var queryString = string.Join("&", queryParams.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
    var fullUrl = $"{url}?{queryString}";

    var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TidalConfig.AccessToken);

    var response = await _httpClient.SendAsync(request, cancellationToken);
    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) {
        // Refresh token and retry once
        // TODO: Implement refresh logic
    }

    return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken) ?? throw new InvalidOperationException("API returned null.");
  }

  public async Task<List<Artist>> SearchArtistsAsync(string query, CancellationToken cancellationToken = default) {
    var response = await GetAsync<TidalSearchContainer>("search", new Dictionary<string, string> { { "query", query }, { "types", "ARTISTS" }, { "limit", "20" } }, cancellationToken);
    return response.Artists?.Items.Select(a => new Artist(a.Id.ToString(), a.Name)).ToList() ?? [];
  }

  public async Task<List<Song>> SearchSongsAsync(string query, CancellationToken cancellationToken = default) {
    var response = await GetAsync<TidalSearchContainer>("search", new Dictionary<string, string> { { "query", query }, { "types", "TRACKS" }, { "limit", "20" } }, cancellationToken);
    return response.Tracks?.Items.Select(MapTrackToSong).ToList() ?? [];
  }

  public async Task<Song> GetSongAsync(string songId, CancellationToken cancellationToken = default) {
    var track = await GetAsync<TidalTrack>($"/tracks/{songId}", null, cancellationToken);
    return MapTrackToSong(track);
  }

  public async Task<Artist> GetArtistAsync(string artistId, CancellationToken cancellationToken = default) {
    var artist = await GetAsync<TidalArtist>($"/artists/{artistId}", null, cancellationToken);
    return new Artist(artist.Id.ToString(), artist.Name);
  }

  public async Task<List<Album>> GetAlbumsByArtistAsync(Artist artist, CancellationToken cancellationToken = default) {
    var response = await GetAsync<TidalSearchResponse<TidalAlbum>>($"/artists/{artist.Id}/albums", new Dictionary<string, string> { { "limit", "50" } }, cancellationToken);
    return response.Items.Select(a => MapAlbumToAlbum(a, artist)).ToList();
  }

  public async Task<List<Song>> GetSongsByAlbumAsync(Album album, CancellationToken cancellationToken = default) {
    var response = await GetAsync<TidalSearchResponse<TidalTrack>>($"/albums/{album.Id}/tracks", new Dictionary<string, string> { { "limit", "50" } }, cancellationToken);
    return response.Items.Select(MapTrackToSong).ToList();
  }

  public async Task<SongStream> GetSongStreamAsync(string songId, CancellationToken cancellationToken = default) {
    var playbackInfo = await GetAsync<TidalPlaybackInfo>($"/tracks/{songId}/playbackinfo", new Dictionary<string, string> { 
        { "audioquality", TidalConfig.AudioQuality },
        { "playbackmode", "STREAM" },
        { "assetpresentation", "FULL" }
    }, cancellationToken);

    var manifestBytes = Convert.FromBase64String(playbackInfo.Manifest);
    var manifestJson = Encoding.UTF8.GetString(manifestBytes);
    var manifest = JsonSerializer.Deserialize<TidalManifest>(manifestJson);

    if (manifest == null || manifest.Urls.Count == 0) throw new InvalidOperationException("No stream URLs found in manifest.");

    var streamUrl = manifest.Urls[0];
    var stream = await _songCacheService.GetOrAddAsync(
        $"{songId}-{playbackInfo.AudioQuality}",
        async ct => await _httpClient.GetStreamAsync(streamUrl, ct),
        cancellationToken);

    return new SongStream(songId, manifest.MimeType.Contains("flac") ? "flac" : "aac", stream);
  }

  public async Task<List<Song>> GetLikedSongsAsync(CancellationToken cancellationToken = default) {
    // Requires user authentication and specific scopes
    return [];
  }

  public async Task<List<Playlist>> SearchPlaylistsAsync(string query, CancellationToken cancellationToken = default) {
    var response = await GetAsync<TidalSearchContainer>("search", new Dictionary<string, string> { { "query", query }, { "types", "PLAYLISTS" }, { "limit", "20" } }, cancellationToken);
    return response.Playlists?.Items.Select(p => new Playlist(p.Uuid, p.Title)).ToList() ?? [];
  }

  public async Task<List<Song>> GetPlaylistSongsAsync(Playlist playlist, CancellationToken cancellationToken = default) {
    var response = await GetAsync<TidalSearchResponse<TidalTrack>>($"/playlists/{playlist.Id}/tracks", new Dictionary<string, string> { { "limit", "50" } }, cancellationToken);
    return response.Items.Select(MapTrackToSong).ToList();
  }

  public async Task<List<Song>> GetPlaylistSongsFromUrlAsync(string url, CancellationToken cancellationToken = default) {
    // Basic URL parsing for Tidal
    if (url.Contains("/track/")) {
        var id = url.Split("/track/").Last().Split('/').First();
        return [await GetSongAsync(id, cancellationToken)];
    }
    if (url.Contains("/playlist/")) {
        var id = url.Split("/playlist/").Last().Split('/').First();
        return await GetPlaylistSongsAsync(new Playlist(id, ""), cancellationToken);
    }
    return [];
  }

  public async Task AddToListenHistory(Song song, CancellationToken cancellationToken = default) {
    await Task.CompletedTask;
  }

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
    return await Image.LoadAsync<Rgba32>(albumArt, cancellationToken);
  }

  private Song MapTrackToSong(TidalTrack track) {
    var artist = new Artist(track.Artist.Id.ToString(), track.Artist.Name);
    var album = MapAlbumToAlbum(track.Album, artist);
    return new Song(track.Id.ToString(), album, track.Title, TimeSpan.FromSeconds(track.Duration), track.TrackNumber);
  }

  private Album MapAlbumToAlbum(TidalAlbum album, Artist artist) {
    var covers = new List<AlbumCover>();
    if (!string.IsNullOrEmpty(album.Cover)) {
        // Tidal covers follow a specific format: https://resources.tidal.com/images/{uuid}/640x640.jpg
        var uuid = album.Cover.Replace("-", "/");
        covers.Add(new AlbumCover($"https://resources.tidal.com/images/{uuid}/640x640.jpg", 640, 640));
        covers.Add(new AlbumCover($"https://resources.tidal.com/images/{uuid}/320x320.jpg", 320, 320));
    }
    return new Album(album.Id.ToString(), artist, album.Title, covers, album.ReleaseDate);
  }

  public void Dispose() {
    if (_isDisposed) return;
    _httpClient.Dispose();
    _isDisposed = true;
  }
}