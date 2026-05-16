using Smoc.Streaming.SoundCloud.Util;

namespace smoc.Tests.Streaming.SoundCloud;

/// <summary>
/// Tests for the <see cref="SoundCloudDiscovery"/> class.
/// </summary>
public class SoundCloudDiscoveryTest {
  /// <summary>
  /// Verifies that script URLs can be extracted from HTML.
  /// </summary>
  [Fact]
  public void ExtractScriptUrls_FindsScripts() {
    var html = "<html><body><script src=\"https://a-v2.sndcdn.com/assets/1.js\"></script><script src=\"https://a-v2.sndcdn.com/assets/2.js\"></script></body></html>";
    var urls = SoundCloudDiscovery.ExtractScriptUrls(html).ToList();
    Assert.Equal(2, urls.Count);
    Assert.Equal("https://a-v2.sndcdn.com/assets/1.js", urls[0]);
    Assert.Equal("https://a-v2.sndcdn.com/assets/2.js", urls[1]);
  }

  /// <summary>
  /// Verifies that the client ID can be extracted from script content.
  /// </summary>
  [Fact]
  public void ExtractClientId_FindsId() {
    var content = "window.Snd={}; Snd.config={client_id:\"0123456789abcdef0123456789abcdef\", ...}";
    var id = SoundCloudDiscovery.ExtractClientId(content);
    Assert.Equal("0123456789abcdef0123456789abcdef", id);
  }

  /// <summary>
  /// Verifies that null is returned if the client ID is not found.
  /// </summary>
  [Fact]
  public void ExtractClientId_NotFound_ReturnsNull() {
    var content = "console.log('hello');";
    var id = SoundCloudDiscovery.ExtractClientId(content);
    Assert.Null(id);
  }
}