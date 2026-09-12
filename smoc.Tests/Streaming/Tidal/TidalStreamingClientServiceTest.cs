using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Moq;
using Moq.Protected;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Smoc.Configuration;
using Smoc.Streaming;
using Smoc.Streaming.Tidal;
using Smoc.Streaming.Tidal.Models;
using Xunit;

namespace smoc.Tests.Streaming.Tidal;

public class TidalStreamingClientServiceTest {
  private static (TidalStreamingClient Client, Mock<HttpMessageHandler> Handler) CreateClientWithMockResponse<T>(T responseObj, HttpStatusCode statusCode = HttpStatusCode.OK) {
    var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
    handlerMock
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>()
      )
      .ReturnsAsync(new HttpResponseMessage {
        StatusCode = statusCode,
        Content = JsonContent.Create(responseObj),
      });

    TidalConfig.Defaults.AccessToken = "test-token";
    TidalConfig.Defaults.TokenExpiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
    TidalConfig.Defaults.ClientId = "test-client";
    TidalConfig.Defaults.RefreshToken = "test-refresh";

    var httpClient = new HttpClient(handlerMock.Object);
    var client = new TidalStreamingClient(httpClient);
    return (client, handlerMock);
  }

  [Fact]
  public async Task SearchSongsAsync_ReturnsSongs() {
    var artist = new TidalArtist(10, "Artist 1", null);
    var album = new TidalAlbum(100, "Album 1", "cover-id", "2023", artist);
    var track = new TidalTrack(1, "Track 1", 180, 1, album, artist, [artist]);
    var response = new TidalSearchContainer(Tracks: new TidalSearchResponse<TidalTrack>([track], 1));

    var (client, _) = CreateClientWithMockResponse(response);

    var results = await client.SearchSongsAsync("query", TestContext.Current.CancellationToken);

    Assert.Single(results);
    Assert.Equal("Track 1", results[0].Title);
    Assert.Equal("1", results[0].Id);
    Assert.Equal("Artist 1", results[0].Artist.Name);
    Assert.Equal("https://resources.tidal.com/images/cover/id/640x640.jpg", results[0].Album.Covers.First().Url);
  }

  [Fact]
  public async Task GetSongStreamAsync_ParsesManifestAndReturnsStream() {
    var manifest = new TidalManifest("audio/flac", "flac", "none", ["http://actual-stream-url"]);
    var manifestJson = JsonSerializer.Serialize(manifest);
    var manifestBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(manifestJson));

    var playbackInfo = new TidalPlaybackInfo(1, "FULL", "LOSSLESS", "application/vnd.tidal.bts", manifestBase64);

    var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

    // First call: GET /tracks/1/playbackinfo
    handlerMock
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/tracks/1/playbackinfo")),
        ItExpr.IsAny<CancellationToken>()
      )
      .ReturnsAsync(new HttpResponseMessage {
        StatusCode = HttpStatusCode.OK,
        Content = JsonContent.Create(playbackInfo),
      });

    // Second call: GET http://actual-stream-url
    handlerMock
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("actual-stream-url")),
        ItExpr.IsAny<CancellationToken>()
      )
      .ReturnsAsync(new HttpResponseMessage {
        StatusCode = HttpStatusCode.OK,
        Content = new ByteArrayContent([1, 2, 3, 4]),
      });

    TidalConfig.Defaults.AccessToken = "test-token";
    TidalConfig.Defaults.TokenExpiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();

    var httpClient = new HttpClient(handlerMock.Object);
    var client = new TidalStreamingClient(httpClient);

    var songStream = await client.GetSongStreamAsync("1", TestContext.Current.CancellationToken);

    Assert.Equal("1", songStream.Id);
    Assert.Equal("flac", songStream.Codec);

    var buffer = new byte[4];
    await songStream.Stream.ReadExactlyAsync(buffer, TestContext.Current.CancellationToken);
    Assert.Equal([1, 2, 3, 4], buffer);
  }

  [Fact]
  public async Task GetSongStreamAsync_UnsupportedManifestMimeType_ThrowsInvalidOperationException() {
    var playbackInfo = new TidalPlaybackInfo(1, "FULL", "LOSSLESS", "application/dash+xml", "invalid-manifest");
    var (client, _) = CreateClientWithMockResponse(playbackInfo);

    await Assert.ThrowsAsync<InvalidOperationException>(() =>
      client.GetSongStreamAsync("1", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task GetSongStreamAsync_InvalidBase64Manifest_ThrowsInvalidOperationException() {
    var playbackInfo = new TidalPlaybackInfo(1, "FULL", "LOSSLESS", "application/vnd.tidal.bts", "not-valid-base64!!!");
    var (client, _) = CreateClientWithMockResponse(playbackInfo);

    await Assert.ThrowsAsync<InvalidOperationException>(() =>
      client.GetSongStreamAsync("1", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task GetSongStreamAsync_EmptyUrlsInManifest_ThrowsInvalidOperationException() {
    var manifest = new TidalManifest("audio/flac", "flac", "none", []);
    var manifestBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest)));
    var playbackInfo = new TidalPlaybackInfo(1, "FULL", "LOSSLESS", "application/vnd.tidal.bts", manifestBase64);
    var (client, _) = CreateClientWithMockResponse(playbackInfo);

    await Assert.ThrowsAsync<InvalidOperationException>(() =>
      client.GetSongStreamAsync("1", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task GetAsync_When401Unauthorized_RefreshesTokenAndRetriesRequest() {
    var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
    var track = new TidalTrack(123, "Refreshed Track", 200, 1, null, null, null);
    var tokenResponse = new TidalTokenResponse("refreshed-access-token", "refreshed-refresh-token", 3600, "Bearer");

    // 1. Initial request with initial token fails with 401
    handlerMock
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get &&
                                            req.RequestUri!.ToString().Contains("/tracks/123") &&
                                            req.Headers.Authorization != null &&
                                            req.Headers.Authorization.Parameter == "initial-expired-token"),
        ItExpr.IsAny<CancellationToken>()
      )
      .ReturnsAsync(new HttpResponseMessage {
        StatusCode = HttpStatusCode.Unauthorized,
      });

    // 2. Token refresh request
    handlerMock
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post && req.RequestUri!.ToString().Contains("/token")),
        ItExpr.IsAny<CancellationToken>()
      )
      .ReturnsAsync(new HttpResponseMessage {
        StatusCode = HttpStatusCode.OK,
        Content = JsonContent.Create(tokenResponse),
      });

    // 3. Retried GET request with new token succeeds
    handlerMock
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get &&
                                            req.RequestUri!.ToString().Contains("/tracks/123") &&
                                            req.Headers.Authorization != null &&
                                            req.Headers.Authorization.Parameter == "refreshed-access-token"),
        ItExpr.IsAny<CancellationToken>()
      )
      .ReturnsAsync(new HttpResponseMessage {
        StatusCode = HttpStatusCode.OK,
        Content = JsonContent.Create(track),
      });

    TidalConfig.Defaults.AccessToken = "initial-expired-token";
    TidalConfig.Defaults.TokenExpiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
    TidalConfig.Defaults.RefreshToken = "initial-refresh-token";
    TidalConfig.Defaults.ClientId = "my-client";

    var httpClient = new HttpClient(handlerMock.Object);
    var client = new TidalStreamingClient(httpClient);

    var song = await client.GetSongAsync("123", TestContext.Current.CancellationToken);

    Assert.Equal("123", song.Id);
    Assert.Equal("Refreshed Track", song.Title);
    Assert.Equal("refreshed-access-token", TidalConfig.Defaults.AccessToken);
    Assert.Equal("refreshed-refresh-token", TidalConfig.Defaults.RefreshToken);
  }

  [Fact]
  public async Task GetPlaylistSongsFromUrlAsync_TrackUrl_ReturnsSong() {
    var artist = new TidalArtist(1, "Track Artist", null);
    var album = new TidalAlbum(10, "Track Album", null, "2022", artist);
    var track = new TidalTrack(12345, "URL Track", 190, 1, album, artist, [artist]);

    var (client, _) = CreateClientWithMockResponse(track);

    var songs = await client.GetPlaylistSongsFromUrlAsync(
      "https://tidal.com/browse/track/12345?si=sample",
      TestContext.Current.CancellationToken);

    Assert.Single(songs);
    Assert.Equal("12345", songs[0].Id);
    Assert.Equal("URL Track", songs[0].Title);
  }

  [Fact]
  public async Task GetPlaylistSongsFromUrlAsync_AlbumUrl_ReturnsSongs() {
    var artist = new TidalArtist(2, "Album Artist", null);
    var album = new TidalAlbum(67890, "URL Album", null, "2021", artist);
    var track = new TidalTrack(987, "Album Track", 150, 1, album, artist, [artist]);
    var searchResponse = new TidalSearchResponse<TidalTrack>([track], 1);

    var (client, _) = CreateClientWithMockResponse(searchResponse);

    var songs = await client.GetPlaylistSongsFromUrlAsync(
      "https://tidal.com/album/67890/",
      TestContext.Current.CancellationToken);

    Assert.Single(songs);
    Assert.Equal("987", songs[0].Id);
  }

  [Fact]
  public async Task GetPlaylistSongsFromUrlAsync_PlaylistUrl_ReturnsSongs() {
    var artist = new TidalArtist(3, "Playlist Artist", null);
    var album = new TidalAlbum(30, "Playlist Album", null, "2020", artist);
    var track = new TidalTrack(54321, "Playlist Track", 210, 1, album, artist, [artist]);
    var searchResponse = new TidalSearchResponse<TidalTrack>([track], 1);

    var (client, _) = CreateClientWithMockResponse(searchResponse);

    var songs = await client.GetPlaylistSongsFromUrlAsync(
      "https://listen.tidal.com/playlist/7e452778-d5a2-4a0e-953e-b49d6350f0ec",
      TestContext.Current.CancellationToken);

    Assert.Single(songs);
    Assert.Equal("54321", songs[0].Id);
  }

  [Fact]
  public async Task GetPlaylistSongsFromUrlAsync_InvalidUrl_ReturnsEmptyList() {
    var (client, _) = CreateClientWithMockResponse(new object());

    var songs = await client.GetPlaylistSongsFromUrlAsync(
      "https://example.com/not-tidal/12345",
      TestContext.Current.CancellationToken);

    Assert.Empty(songs);
  }

  [Fact]
  public async Task GetAlbumArtAsync_ReturnsImageOnSuccess() {
    using var image = new Image<Rgba32>(16, 16);
    using var stream = new MemoryStream();
    await image.SaveAsPngAsync(stream, TestContext.Current.CancellationToken);
    var contentBytes = stream.ToArray();

    var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
    handlerMock
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("cover.jpg")),
        ItExpr.IsAny<CancellationToken>()
      )
      .ReturnsAsync(new HttpResponseMessage {
        StatusCode = HttpStatusCode.OK,
        Content = new ByteArrayContent(contentBytes),
      });

    var httpClient = new HttpClient(handlerMock.Object);
    var client = new TidalStreamingClient(httpClient);

    var album = new Album(
      "alb-1",
      new Artist("art-1", "Artist"),
      "Album",
      [new AlbumCover("https://resources.tidal.com/images/cover.jpg", 16, 16)]);

    var resultImage = await client.GetAlbumArtAsync(album, null, TestContext.Current.CancellationToken);

    Assert.NotNull(resultImage);
    Assert.Equal(16, resultImage.Width);
    Assert.Equal(16, resultImage.Height);
  }

  [Fact]
  public async Task GetAlbumArtAsync_ThrowsIfNoCovers() {
    var (client, _) = CreateClientWithMockResponse(new object());
    var album = new Album("alb-2", new Artist("art-2", "Artist"), "Album Without Covers", []);

    await Assert.ThrowsAsync<ArgumentException>(() =>
      client.GetAlbumArtAsync(album, null, TestContext.Current.CancellationToken));
  }
}
