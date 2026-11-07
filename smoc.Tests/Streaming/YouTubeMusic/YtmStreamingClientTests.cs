using System.Net;
using Smoc.Streaming.YouTubeMusic;
using Xunit;

namespace smoc.Tests.Streaming.YouTubeMusic;

public class YtmStreamingClientTests {
  [Fact]
  public void ParseCookies_ValidCookieString_ParsesExpectedCookies() {
    string cookieHeader = "SID=abc123; HSID=def456; SSID=ghi789;";

    List<Cookie> cookies = YtmStreamingClient.ParseCookies(cookieHeader);

    Assert.Equal(3, cookies.Count);
    Assert.Equal("SID", cookies[0].Name);
    Assert.Equal("abc123", cookies[0].Value);
    Assert.Equal(".youtube.com", cookies[0].Domain);
    Assert.Equal("/", cookies[0].Path);

    Assert.Equal("HSID", cookies[1].Name);
    Assert.Equal("def456", cookies[1].Value);

    Assert.Equal("SSID", cookies[2].Name);
    Assert.Equal("ghi789", cookies[2].Value);
  }

  [Fact]
  public void ParseCookies_WhitespaceAndMalformedEntries_HandlesGracefully() {
    string cookieHeader = "  SID = abc123 ; ; malformed_no_equals ; HSID=xyz = extra ;   ";

    List<Cookie> cookies = YtmStreamingClient.ParseCookies(cookieHeader);

    Assert.Equal(2, cookies.Count);
    Assert.Equal("SID", cookies[0].Name);
    Assert.Equal("abc123", cookies[0].Value);

    Assert.Equal("HSID", cookies[1].Name);
    Assert.Equal("xyz = extra", cookies[1].Value);
  }

  [Fact]
  public void ParseCookies_EmptyString_ReturnsEmptyList() {
    List<Cookie> cookies = YtmStreamingClient.ParseCookies(string.Empty);

    Assert.Empty(cookies);
  }
}