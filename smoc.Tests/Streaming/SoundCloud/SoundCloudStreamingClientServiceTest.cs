using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Smoc.Configuration;
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

  /// <summary>
  /// Verifies that when an auth token is provided, the OAuth header is attached only to api-v2.soundcloud.com requests.
  /// </summary>
  [Fact]
  public async Task SearchSongsAsync_WhenAuthTokenProvided_AttachesOAuthHeaderToApiRequests() {
    var response = new SoundCloudSearchResponse<SoundCloudTrack>([], null);
    var json = JsonSerializer.Serialize(response);
    AuthenticationHeaderValue? capturedAuth = null;

    var handler = new MockHandler {
      Handler = req => {
        capturedAuth = req.Headers.Authorization;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
          Content = new StringContent(json)
        });
      }
    };

    var httpClient = new HttpClient(handler);
    var client = SoundCloudStreamingClient.CreateForTesting(httpClient, authToken: "oauth-token-123");

    await client.SearchSongsAsync("query", TestContext.Current.CancellationToken);

    Assert.NotNull(capturedAuth);
    Assert.Equal("OAuth", capturedAuth.Scheme);
    Assert.Equal("oauth-token-123", capturedAuth.Parameter);
  }

  /// <summary>
  /// Verifies that the OAuth token is NOT sent to external CDN or third-party stream URLs.
  /// </summary>
  [Fact]
  public async Task GetSongStreamAsync_WhenAuthTokenProvided_DoesNotAttachOAuthHeaderToCdnStreamRequest() {
    var track = new SoundCloudTrack(1, "Track 1", 1000, null, new SoundCloudUser(10, "Artist 1", "http://avatar"), new SoundCloudMedia(
        [
            new SoundCloudTranscoding("https://api-v2.soundcloud.com/media/stream-meta", "mp3", new SoundCloudFormat("progressive", "audio/mpeg"))
        ]
    ));
    var streamInfo = new SoundCloudStreamResponse("https://cf-media.sndcdn.com/actual-stream-url");

    AuthenticationHeaderValue? apiAuth = null;
    AuthenticationHeaderValue? cdnAuth = null;

    var handler = new MockHandler {
      Handler = req => {
        var url = req.RequestUri!.ToString();
        if (url.Contains("/tracks/1") || url.Contains("stream-meta")) {
          apiAuth = req.Headers.Authorization;
          var body = url.Contains("/tracks/1") ? JsonSerializer.Serialize(track) : JsonSerializer.Serialize(streamInfo);
          return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(body)
          });
        }
        if (url.Contains("actual-stream-url")) {
          cdnAuth = req.Headers.Authorization;
          return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new ByteArrayContent([1, 2, 3, 4])
          });
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
      }
    };

    var httpClient = new HttpClient(handler);
    var client = SoundCloudStreamingClient.CreateForTesting(httpClient, authToken: "secret-oauth-token");

    var songStream = await client.GetSongStreamAsync("1", TestContext.Current.CancellationToken);
    Assert.NotNull(songStream);

    Assert.NotNull(apiAuth);
    Assert.Equal("secret-oauth-token", apiAuth.Parameter);
    Assert.Null(cdnAuth);
  }

  /// <summary>
  /// Verifies that the OAuth token is NOT sent to a look-alike domain that starts with api-v2.soundcloud.com.
  /// </summary>
  [Fact]
  public async Task GetSongStreamAsync_WhenAuthTokenProvided_DoesNotAttachOAuthHeaderToLookAlikeHost() {
    var track = new SoundCloudTrack(1, "Track 1", 1000, null, new SoundCloudUser(10, "Artist 1", "http://avatar"), new SoundCloudMedia(
        [
            new SoundCloudTranscoding("https://api-v2.soundcloud.com.attacker.tld/media/stream-meta", "mp3", new SoundCloudFormat("progressive", "audio/mpeg"))
        ]
    ));
    var streamInfo = new SoundCloudStreamResponse("https://cf-media.sndcdn.com/actual-stream-url");

    AuthenticationHeaderValue? lookAlikeHostAuth = null;

    var handler = new MockHandler {
      Handler = req => {
        var url = req.RequestUri!.ToString();
        if (url.Contains("/tracks/1")) {
          return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(JsonSerializer.Serialize(track))
          });
        }
        if (url.Contains("attacker.tld")) {
          lookAlikeHostAuth = req.Headers.Authorization;
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
    var client = SoundCloudStreamingClient.CreateForTesting(httpClient, authToken: "secret-oauth-token");

    var songStream = await client.GetSongStreamAsync("1", TestContext.Current.CancellationToken);
    Assert.NotNull(songStream);
    Assert.Null(lookAlikeHostAuth);
  }

  /// <summary>
  /// Verifies that when no auth token is provided, requests do not contain an Authorization header.
  /// </summary>
  [Fact]
  public async Task SearchSongsAsync_WhenNoAuthToken_DoesNotAttachAuthHeader() {
    var response = new SoundCloudSearchResponse<SoundCloudTrack>([], null);
    var json = JsonSerializer.Serialize(response);
    AuthenticationHeaderValue? capturedAuth = null;

    var handler = new MockHandler {
      Handler = req => {
        capturedAuth = req.Headers.Authorization;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
          Content = new StringContent(json)
        });
      }
    };

    var httpClient = new HttpClient(handler);
    var client = SoundCloudStreamingClient.CreateForTesting(httpClient, authToken: null);

    await client.SearchSongsAsync("query", TestContext.Current.CancellationToken);

    Assert.Null(capturedAuth);
  }
}
