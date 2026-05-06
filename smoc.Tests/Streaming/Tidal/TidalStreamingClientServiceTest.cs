using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Moq;
using Moq.Protected;
using Smoc.Configuration;
using Smoc.Streaming;
using Smoc.Streaming.Tidal;
using Smoc.Streaming.Tidal.Models;
using Smoc.Services.Caching;

namespace smoc.Tests.Streaming.Tidal;

public class TidalStreamingClientServiceTest {
  private static (TidalStreamingClient, Mock<HttpMessageHandler>) CreateClientWithMockResponse<T>(T responseObj, HttpStatusCode statusCode = HttpStatusCode.OK) {
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

    var httpClient = new HttpClient(handlerMock.Object);
    var client = TidalStreamingClient.CreateForTesting(httpClient);
    return (client, handlerMock);
  }

  [Fact]
  public async Task SearchSongsAsync_ReturnsSongs() {
    var artist = new TidalArtist(10, "Artist 1", null);
    var album = new TidalAlbum(100, "Album 1", "cover-id", "2023", artist);
    var track = new TidalTrack(1, "Track 1", 180, 1, album, artist, [artist]);
    var response = new TidalSearchContainer {
        Tracks = new TidalSearchResponse<TidalTrack> { Items = [track], TotalNumberOfItems = 1 }
    };
    
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
    var manifest = new TidalManifest {
        MimeType = "audio/flac",
        Codecs = "flac",
        EncryptionType = "none",
        Urls = ["http://actual-stream-url"]
    };
    var manifestJson = JsonSerializer.Serialize(manifest);
    var manifestBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(manifestJson));
    
    var playbackInfo = new TidalPlaybackInfo(1, "FULL", "LOSSLESS", "application/vnd.tidal.bt", manifestBase64);

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
        ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == "http://actual-stream-url"),
        ItExpr.IsAny<CancellationToken>()
      )
      .ReturnsAsync(new HttpResponseMessage {
        StatusCode = HttpStatusCode.OK,
        Content = new ByteArrayContent([1, 2, 3, 4]),
      });

    var httpClient = new HttpClient(handlerMock.Object);
    var client = TidalStreamingClient.CreateForTesting(httpClient);
    
    var songStream = await client.GetSongStreamAsync("1", TestContext.Current.CancellationToken);

    Assert.Equal("1", songStream.Id);
    Assert.Equal("flac", songStream.Codec);
    
    var buffer = new byte[4];
    await songStream.Stream.ReadAsync(buffer, TestContext.Current.CancellationToken);
    Assert.Equal([1, 2, 3, 4], buffer);
  }
}