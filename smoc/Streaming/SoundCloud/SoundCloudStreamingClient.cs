using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Terminal.Gui.App;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Smoc.Configuration;
using Smoc.Services;
using Smoc.Services.Caching;
using Smoc.Streaming.SoundCloud.Models;
using Smoc.Streaming.SoundCloud.Util;
using Smoc.Ui.Drawing;

namespace Smoc.Streaming.SoundCloud;

/// <summary>
/// A streaming client for SoundCloud.
/// </summary>
public sealed class SoundCloudStreamingClient : IStreamingClient {
  private static readonly string SoundCloudUrl = "https://soundcloud.com";
  private static readonly string ApiUrl = "https://api-v2.soundcloud.com";
  private readonly HttpClient _httpClient;
  private readonly ICacheService _songCacheService;
  private readonly ICacheService _albumArtCacheService;
  private string? _clientId;

  private SoundCloudStreamingClient(ICacheService? songCacheService = null, ICacheService? albumArtCacheService = null) {
    _httpClient = new HttpClient();
    _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    _songCacheService = songCacheService ?? new NoCachingCacheService();
    _albumArtCacheService = albumArtCacheService ?? new NoCachingCacheService();
    _clientId = SoundCloudConfig.ClientId;
  }

  /// <summary>
  /// Creates a new instance of <see cref="SoundCloudStreamingClient"/> for testing.
  /// </summary>
  /// <param name="httpClient">The HTTP client to use.</param>
  /// <param name="songCacheService">The song cache service.</param>
  /// <param name="albumArtCacheService">The album art cache service.</param>
  /// <param name="clientId">The SoundCloud client ID.</param>
  /// <returns>A new instance of <see cref="SoundCloudStreamingClient"/>.</returns>
  public static SoundCloudStreamingClient CreateForTesting(HttpClient httpClient, ICacheService? songCacheService = null, ICacheService? albumArtCacheService = null, string? clientId = "test-client-id") {
    var client = new SoundCloudStreamingClient(songCacheService, albumArtCacheService);
    var httpClientField = typeof(SoundCloudStreamingClient).GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    httpClientField?.SetValue(client, httpClient);
    client._clientId = clientId;
    return client;
  }

  /// <summary>
  /// Creates a new instance of <see cref="SoundCloudStreamingClient"/>.
  /// </summary>
  /// <param name="songCacheService">The song cache service.</param>
  /// <param name="albumArtCacheService">The album art cache service.</param>
  /// <returns>A new instance of <see cref="SoundCloudStreamingClient"/>.</returns>
  public static SoundCloudStreamingClient Create(ICacheService? songCacheService = null, ICacheService? albumArtCacheService = null) {
    return new SoundCloudStreamingClient(songCacheService, albumArtCacheService);
  }

  private async Task<string> GetClientIdAsync(CancellationToken cancellationToken = default) {
    if (!string.IsNullOrEmpty(_clientId)) {
      return _clientId;
    }

    Logging.Information("Discovering SoundCloud Client ID...");
    var response = await _httpClient.GetStringAsync(SoundCloudUrl, cancellationToken);
    
    foreach (var scriptUrl in SoundCloudDiscovery.ExtractScriptUrls(response)) {
      if (!scriptUrl.StartsWith("http")) {
        continue;
      }

      var scriptContent = await _httpClient.GetStringAsync(scriptUrl, cancellationToken);
      var clientId = SoundCloudDiscovery.ExtractClientId(scriptContent);
      if (clientId != null) {
        _clientId = clientId;
        Logging.Information($"Discovered SoundCloud Client ID: {_clientId}");
        return _clientId;
      }
    }

    throw new InvalidOperationException("Could not discover SoundCloud Client ID.");
  }

  private async Task<T> GetAsync<T>(string endpoint, Dictionary<string, string>? parameters = null, CancellationToken cancellationToken = default) {
    var clientId = await GetClientIdAsync(cancellationToken);
    var url = endpoint.StartsWith("http") ? endpoint : $"{ApiUrl}/{endpoint.TrimStart('/')}";
    var queryParams = parameters ?? new Dictionary<string, string>();
    queryParams["client_id"] = clientId;
    
    var queryString = string.Join("&", queryParams.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
    var fullUrl = url.Contains('?') ? $"{url}&{queryString}" : $"{url}?{queryString}";

    return await _httpClient.GetFromJsonAsync<T>(fullUrl, cancellationToken) ?? throw new InvalidOperationException("API returned null.");
  }

  /// <inheritdoc />
  public async Task<List<Artist>> SearchArtistsAsync(string query, CancellationToken cancellationToken = default) {
    var response = await GetAsync<SoundCloudSearchResponse<SoundCloudUser>>("search/users", new Dictionary<string, string> { { "q", query }, { "limit", "20" } }, cancellationToken);
    return response.Collection.Select(u => new Artist(u.Id.ToString(), u.Username)).ToList();
  }

  /// <inheritdoc />
  public async Task<List<Song>> SearchSongsAsync(string query, CancellationToken cancellationToken = default) {
    var response = await GetAsync<SoundCloudSearchResponse<SoundCloudTrack>>("search/tracks", new Dictionary<string, string> { { "q", query }, { "limit", "20" } }, cancellationToken);
    return response.Collection.Select(SoundCloudMapper.MapTrackToSong).ToList();
  }

  /// <inheritdoc />
  public async Task<Song> GetSongAsync(string songId, CancellationToken cancellationToken = default) {
    var track = await GetAsync<SoundCloudTrack>($"/tracks/{songId}", null, cancellationToken);
    return SoundCloudMapper.MapTrackToSong(track);
  }

  /// <inheritdoc />
  public async Task<Artist> GetArtistAsync(string artistId, CancellationToken cancellationToken = default) {
    var user = await GetAsync<SoundCloudUser>($"/users/{artistId}", null, cancellationToken);
    return new Artist(user.Id.ToString(), user.Username);
  }

  /// <inheritdoc />
  public async Task<List<Album>> GetAlbumsByArtistAsync(Artist artist, CancellationToken cancellationToken = default) {
    // SoundCloud doesn't have a direct "albums" for all artists that map 1:1.
    // We'll treat "SoundCloud Uploads" as a default album.
    return [new Album($"sc-uploads-{artist.Id}", artist, "SoundCloud Uploads", [])];
  }

  /// <inheritdoc />
  public async Task<List<Song>> GetSongsByAlbumAsync(Album album, CancellationToken cancellationToken = default) {
    if (album.Id.StartsWith("sc-uploads-")) {
      var userId = album.Id.Replace("sc-uploads-", "");
      var response = await GetAsync<SoundCloudSearchResponse<SoundCloudTrack>>($"/users/{userId}/tracks", new Dictionary<string, string> { { "limit", "50" } }, cancellationToken);
      return response.Collection.Select(SoundCloudMapper.MapTrackToSong).ToList();
    }
    return [];
  }

  /// <inheritdoc />
  public async Task<SongStream> GetSongStreamAsync(string songId, CancellationToken cancellationToken = default) {
    var track = await GetAsync<SoundCloudTrack>($"/tracks/{songId}", null, cancellationToken);
    
    var transcoding = track.Media.Transcodings.FirstOrDefault(t => t.Format.Protocol == "progressive" && t.Format.MimeType == "audio/mpeg")
                      ?? track.Media.Transcodings.FirstOrDefault(t => t.Format.Protocol == "hls" && t.Format.MimeType == "audio/mpeg")
                      ?? track.Media.Transcodings.FirstOrDefault();

    if (transcoding == null) {
      throw new InvalidOperationException("No playable transcoding found.");
    }

    var streamResponse = await GetAsync<SoundCloudStreamResponse>(transcoding.Url, null, cancellationToken);
    
    var stream = await _songCacheService.GetOrAddAsync(
      $"{songId}-{transcoding.Preset}",
      async ct => await _httpClient.GetStreamAsync(streamResponse.Url, ct),
      cancellationToken);

    var codec = transcoding.Format.Protocol == "hls" ? "hls" : "mp3";
    return new SongStream(songId, codec, stream);
  }

  /// <inheritdoc />
  public async Task<List<Song>> GetLikedSongsAsync(CancellationToken cancellationToken = default) {
    // Guest access doesn't support likes. Phase 2 would require AuthToken.
    return [];
  }

  /// <inheritdoc />
  public async Task<List<Playlist>> SearchPlaylistsAsync(string query, CancellationToken cancellationToken = default) {
    var response = await GetAsync<SoundCloudSearchResponse<SoundCloudPlaylist>>("search/playlists", new Dictionary<string, string> { { "q", query }, { "limit", "20" } }, cancellationToken);
    return response.Collection.Select(p => new Playlist(p.Id.ToString(), p.Title)).ToList();
  }

  /// <inheritdoc />
  public async Task<List<Song>> GetPlaylistSongsAsync(Playlist playlist, CancellationToken cancellationToken = default) {
    var scPlaylist = await GetAsync<SoundCloudPlaylist>($"/playlists/{playlist.Id}", null, cancellationToken);
    return scPlaylist.Tracks.Select(SoundCloudMapper.MapTrackToSong).ToList();
  }

  /// <inheritdoc />
  public async Task<List<Song>> GetPlaylistSongsFromUrlAsync(string url, CancellationToken cancellationToken = default) {
    var result = await GetAsync<System.Text.Json.JsonElement>("resolve", new Dictionary<string, string> { { "url", url } }, cancellationToken);
    
    if (result.TryGetProperty("kind", out var kind)) {
        var kindStr = kind.GetString();
        var json = result.GetRawText();
        if (kindStr == "playlist") {
            var playlist = System.Text.Json.JsonSerializer.Deserialize<SoundCloudPlaylist>(json);
            return playlist?.Tracks.Select(SoundCloudMapper.MapTrackToSong).ToList() ?? [];
        } else if (kindStr == "track") {
            var track = System.Text.Json.JsonSerializer.Deserialize<SoundCloudTrack>(json);
            return track != null ? [SoundCloudMapper.MapTrackToSong(track)] : [];
        }
    }
    
    return [];
  }

  /// <inheritdoc />
  public async Task AddToListenHistory(Song song, CancellationToken cancellationToken = default) {
     await Task.CompletedTask;
  }

  /// <inheritdoc />
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

  }