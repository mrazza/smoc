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

  [Fact]
  public async Task HandleRequest_PostMethod_ReturnsMethodNotAllowed() {
    var stream = new MemoryStream(new byte[] { 1, 2, 3 });
    var url = _sut.StartProxy(stream, "audio/mpeg");

    var response = await _httpClient.PostAsync(url, new StringContent("test"), TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
  }

  [Fact]
  public async Task HandleRequest_InvalidStreamId_ReturnsNotFound() {
    var stream = new MemoryStream(new byte[] { 1, 2, 3 });
    var url = _sut.StartProxy(stream, "audio/mpeg");
    var nonExistentUrl = url + "-non-existent";

    var response = await _httpClient.GetAsync(nonExistentUrl, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task HandleRequest_HeadMethod_ReturnsHeadersWithZeroBody() {
    var data = new byte[] { 1, 2, 3, 4, 5 };
    var stream = new MemoryStream(data);
    var url = _sut.StartProxy(stream, "audio/mpeg");

    var request = new HttpRequestMessage(HttpMethod.Head, url);
    var response = await _httpClient.SendAsync(request, TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("audio/mpeg", response.Content.Headers.ContentType?.MediaType);
    var body = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
    Assert.Empty(body);
  }

  [Fact]
  public async Task HandleRequest_InvalidRange_ReturnsRangeNotSatisfiable() {
    var data = new byte[] { 1, 2, 3 };
    var stream = new MemoryStream(data);
    var url = _sut.StartProxy(stream, "audio/mpeg");

    var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Range = new RangeHeaderValue(100, 200);

    var response = await _httpClient.SendAsync(request, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, response.StatusCode);
  }

  [Fact]
  public async Task StopProxy_RemovesStreamEndpoint() {
    var stream = new MemoryStream(new byte[] { 1, 2, 3 });
    var url = _sut.StartProxy(stream, "audio/mpeg");

    _sut.StopProxy(url);

    var response = await _httpClient.GetAsync(url, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public void StartProxy_WithTargetHost_ResolvesLocalIpViaUdpProbe() {
    var stream = new MemoryStream(new byte[] { 1, 2, 3 });
    var url = _sut.StartProxy(stream, "audio/mpeg", "8.8.8.8");

    Assert.NotNull(url);
    Assert.StartsWith("http://", url);
    Assert.Equal(url, _sut.CurrentProxyUrl);
  }

  [Fact]
  public async Task Proxy_ServesSuffixByteRange() {
    var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
    var stream = new MemoryStream(data);
    var url = _sut.StartProxy(stream, "audio/mpeg");

    var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Range = new RangeHeaderValue(null, 3); // bytes=-3 (last 3 bytes)

    var response = await _httpClient.SendAsync(request, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
    Assert.Equal("bytes 7-9/10", response.Content.Headers.ContentRange?.ToString());

    var responseData = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal(new byte[] { 7, 8, 9 }, responseData);
  }

  [Fact]
  public async Task Proxy_ServesFromOffsetToByteEnd() {
    var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
    var stream = new MemoryStream(data);
    var url = _sut.StartProxy(stream, "audio/mpeg");

    var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Range = new RangeHeaderValue(7, null); // bytes=7-

    var response = await _httpClient.SendAsync(request, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
    Assert.Equal("bytes 7-9/10", response.Content.Headers.ContentRange?.ToString());

    var responseData = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal(new byte[] { 7, 8, 9 }, responseData);
  }

  [Fact]
  public async Task Proxy_ServesNonSeekableStream_WithChunkedTransfer() {
    var data = new byte[] { 1, 2, 3, 4, 5 };
    using var memStream = new MemoryStream(data);
    using var nonSeekableStream = new NonSeekableStreamWrapper(memStream);
    var url = _sut.StartProxy(nonSeekableStream, "audio/mpeg");

    var response = await _httpClient.GetAsync(url, TestContext.Current.CancellationToken);
    Assert.True(response.IsSuccessStatusCode);
    Assert.True(response.Headers.TransferEncodingChunked ?? false);

    var responseData = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
    Assert.Equal(data, responseData);
  }

  private sealed class NonSeekableStreamWrapper(Stream inner) : Stream {
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => throw new NotSupportedException();
    public override long Position {
      get => throw new NotSupportedException();
      set => throw new NotSupportedException();
    }
    public override void Flush() => inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
  }

  public void Dispose() {
    _sut.Dispose();
    _httpClient.Dispose();
  }
}
