
using System.Net;
using System.Net.Http.Json;
using Moq;
using Moq.Protected;
using Smoc.Streaming;
using Smoc.Streaming.Subsonic;
using Xunit;

namespace smoc.Tests.Streaming.Subsonic;

public class SubsonicStreamingClientServiceTest {
  [Fact]
  public async Task SearchArtistsAsync_ReturnsArtistsOnSuccess() {
    // Arrange
    var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""ok"",
        ""version"": ""1.16.1"",
        ""searchResult3"": {
          ""artist"": [
            { ""id"": ""a1"", ""name"": ""Artist One"" },
            { ""id"": ""a2"", ""name"": ""Artist Two"" }
          ]
        }
      }
    }";

    handlerMock
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>()
      )
      .ReturnsAsync(new HttpResponseMessage {
        StatusCode = HttpStatusCode.OK,
        Content = new StringContent(jsonResponse),
      });

    var httpClient = new HttpClient(handlerMock.Object);
    var client = SubsonicStreamingClient.CreateForTesting("http://localhost", "user", "pass", true);
    
    // Inject the mocked httpClient via reflection for testing
    var httpClientField = typeof(SubsonicStreamingClient).GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    httpClientField?.SetValue(client, httpClient);

    // Act
    var results = await client.SearchArtistsAsync("Artist");

    // Assert
    Assert.Equal(2, results.Count);
    Assert.Equal("Artist One", results[0].Name);
    Assert.Equal("a1", results[0].Id);
  }

  [Fact]
  public async Task GetAsync_ThrowsOnSubsonicError() {
    // Arrange
    var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
    var jsonResponse = @"{
      ""subsonic-response"": {
        ""status"": ""failed"",
        ""version"": ""1.16.1"",
        ""error"": { ""code"": 40, ""message"": ""Wrong user or password"" }
      }
    }";

    handlerMock
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>()
      )
      .ReturnsAsync(new HttpResponseMessage {
        StatusCode = HttpStatusCode.OK, // Subsonic often returns 200 even on API errors
        Content = new StringContent(jsonResponse),
      });

    var httpClient = new HttpClient(handlerMock.Object);
    var client = SubsonicStreamingClient.CreateForTesting("http://localhost", "user", "pass", true);
    var httpClientField = typeof(SubsonicStreamingClient).GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    httpClientField?.SetValue(client, httpClient);

    // Act & Assert
    var ex = await Assert.ThrowsAsync<Exception>(() => client.SearchArtistsAsync("Artist"));
    Assert.Contains("error 40: Wrong user or password", ex.Message);
  }
}
