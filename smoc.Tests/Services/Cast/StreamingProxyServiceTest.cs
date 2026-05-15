using Smoc.Services.Cast;
using System.Net.Http;

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
        Assert.EndsWith("/stream", url);
    }

    [Fact]
    public async Task Proxy_ServesStreamContent() {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var stream = new MemoryStream(data);
        var url = _sut.StartProxy(stream, "audio/mpeg");

        var response = await _httpClient.GetAsync(url);
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("audio/mpeg", response.Content.Headers.ContentType?.MediaType);
        
        var responseData = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(data, responseData);
    }

    public void Dispose() {
        _sut.Dispose();
        _httpClient.Dispose();
    }
}