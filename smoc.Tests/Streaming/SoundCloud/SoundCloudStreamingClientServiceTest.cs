using System.Net;
using System.Text.Json;
using Smoc.Streaming.SoundCloud;
using Smoc.Streaming.SoundCloud.Models;

namespace smoc.Tests.Streaming.SoundCloud;

/// <summary>
/// Tests for the <see cref="SoundCloudStreamingClient"/> class.
/// </summary>
public class SoundCloudStreamingClientServiceTest {
  private class MockHandler : HttpMessageHandler {
    public Func<HttpRequestMessage, Task<HttpResponseMessage>> Handler { get; set; } = req => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Handler(request);
  }

  /// <summary>
  /// Verifies that songs can be searched.
  /// </summary>
  [Fact]
  public async Task SearchSongsAsync_ReturnsSongs() {
    var response = new SoundCloudSearchResponse<SoundCloudTrack>(
        [
            new SoundCloudTrack(1, "Track 1", 1000, null, new SoundCloudUser(10, "Artist 1", "http://avatar"), new SoundCloudMedia([]))
        ],
        null
    );
    var json = JsonSerializer.Serialize(response);

    var handler = new MockHandler {
      Handler = req => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
        Content = new StringContent(json)
      })
    };

    var httpClient = new HttpClient(handler);
    var client = SoundCloudStreamingClient.CreateForTesting(httpClient);

    var results = await client.SearchSongsAsync("query", TestContext.Current.CancellationToken);

    Assert.Single(results);
    Assert.Equal("Track 1", results[0].Title);
    Assert.Equal("1", results[0].Id);
  }

  /// <summary>
  /// Verifies that a song stream can be retrieved.
  /// </summary>
  [Fact]
  public async Task GetSongStreamAsync_ReturnsStream() {
    var track = new SoundCloudTrack(1, "Track 1", 1000, null, new SoundCloudUser(10, "Artist 1", "http://avatar"), new SoundCloudMedia(
        [
            new SoundCloudTranscoding("http://stream-meta/v1", "mp3", new SoundCloudFormat("progressive", "audio/mpeg"))
        ]
    ));
    var streamInfo = new SoundCloudStreamResponse("http://actual-stream-url");

    var handler = new MockHandler {
      Handler = req => {
        var url = req.RequestUri!.ToString();
        if (url.Contains("/tracks/1")) {
          return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(JsonSerializer.Serialize(track))
          });
        }
        if (url.Contains("stream-meta")) {
          return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(JsonSerializer.Serialize(streamInfo))
          });
        }
        if (url.Contains("actual-stream-url")) {
          return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new ByteArrayContent([1, 2, 3, 4])
          });
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
      }
    };

    var httpClient = new HttpClient(handler);
    var client = SoundCloudStreamingClient.CreateForTesting(httpClient);

    var songStream = await client.GetSongStreamAsync("1", TestContext.Current.CancellationToken);

    Assert.Equal("1", songStream.Id);
    Assert.Equal("mp3", songStream.Codec);

    var buffer = new byte[4];
    var read = await songStream.Stream.ReadAsync(buffer, TestContext.Current.CancellationToken);
    Assert.Equal(4, read);
    Assert.Equal([1, 2, 3, 4], buffer);
  }
}