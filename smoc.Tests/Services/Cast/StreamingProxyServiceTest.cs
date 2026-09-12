using Smoc.Services.Cast;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace smoc.Tests.Services.Cast;

public class StreamingProxyServiceTest : IDisposable {
  private readonly StreamingProxyService _sut;
  private readonly HttpClient _httpClient;

  public StreamingProxyServiceTest() {
    _sut = new StreamingProxyService();
    _httpClient = new HttpClient();
  }

  [Fact]
  public async Task StartProxy_ReturnsValidUrl() {
    var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
    var url = _sut.StartProxy(stream, "audio/mpeg");

    Assert.NotNull(url);
    Assert.StartsWith("http://", url);
    Assert.Contains("/stream/", url);
  }

  [Fact]
  public async Task Proxy_ServesStreamContent() {
    var data = new byte[] { 1, 2, 3, 4, 5 };
    var stream = new MemoryStream(data);
    var url = _sut.StartProxy(stream, "audio/mpeg");

    var response = await _httpClient.GetAsync(url, TestContext.Current.CancellationToken);
    Assert.True(response.IsSuccessStatusCode);
    Assert.Equal("audio/mpeg", response.Content.Headers.ContentType?.MediaType);

    var responseData = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal(data, responseData);
  }

  [Fact]
  public async Task Proxy_ServesStreamContent_MultipleTimes() {
    var data = new byte[] { 1, 2, 3, 4, 5 };
    var stream = new MemoryStream(data);
    var url = _sut.StartProxy(stream, "audio/mpeg");

    // First request
    var response1 = await _httpClient.GetAsync(url, TestContext.Current.CancellationToken);
    Assert.True(response1.IsSuccessStatusCode);
    var responseData1 = await response1.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal(data, responseData1);

    // Second request (should succeed and seek back to beginning)
    var response2 = await _httpClient.GetAsync(url, TestContext.Current.CancellationToken);
    Assert.True(response2.IsSuccessStatusCode);
    var responseData2 = await response2.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal(data, responseData2);
  }

  [Fact]
  public async Task Proxy_SupportsConcurrentStreams() {
    var data1 = new byte[] { 10, 20, 30 };
    var data2 = new byte[] { 40, 50, 60, 70 };
    var stream1 = new MemoryStream(data1);
    var stream2 = new MemoryStream(data2);

    var url1 = _sut.StartProxy(stream1, "audio/mpeg");
    var url2 = _sut.StartProxy(stream2, "audio/flac");

    var response1 = await _httpClient.GetAsync(url1, TestContext.Current.CancellationToken);
    var response2 = await _httpClient.GetAsync(url2, TestContext.Current.CancellationToken);

    Assert.True(response1.IsSuccessStatusCode);
    Assert.True(response2.IsSuccessStatusCode);
    Assert.Equal("audio/mpeg", response1.Content.Headers.ContentType?.MediaType);
    Assert.Equal("audio/flac", response2.Content.Headers.ContentType?.MediaType);

    var bytes1 = await response1.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
    var bytes2 = await response2.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

    Assert.Equal(data1, bytes1);
    Assert.Equal(data2, bytes2);
  }

  [Fact]
  public async Task Proxy_ServesRangeRequests() {
    var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
    var stream = new MemoryStream(data);
    var url = _sut.StartProxy(stream, "audio/mpeg");

    var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Range = new RangeHeaderValue(2, 5);

    var response = await _httpClient.SendAsync(request, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
    Assert.Equal("bytes 2-5/10", response.Content.Headers.ContentRange?.ToString());

    var responseData = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal(new byte[] { 2, 3, 4, 5 }, responseData);
  }

  public void Dispose() {
    _sut.Dispose();
    _httpClient.Dispose();
  }
}
