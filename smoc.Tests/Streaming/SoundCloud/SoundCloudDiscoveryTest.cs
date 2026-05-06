using Smoc.Streaming.SoundCloud.Util;

namespace smoc.Tests.Streaming.SoundCloud;

public class SoundCloudDiscoveryTest {
  [Fact]
  public void ExtractScriptUrls_FindsScripts() {
    var html = "<html><body><script src=\"https://a-v2.sndcdn.com/assets/1.js\"></script><script src=\"https://a-v2.sndcdn.com/assets/2.js\"></script></body></html>";
    var urls = SoundCloudDiscovery.ExtractScriptUrls(html).ToList();
    Assert.Equal(2, urls.Count);
    Assert.Equal("https://a-v2.sndcdn.com/assets/1.js", urls[0]);
    Assert.Equal("https://a-v2.sndcdn.com/assets/2.js", urls[1]);
  }

  [Fact]
  public void ExtractClientId_FindsId() {
    var content = "window.Snd={}; Snd.config={client_id:\"0123456789abcdef0123456789abcdef\", ...}";
    var id = SoundCloudDiscovery.ExtractClientId(content);
    Assert.Equal("0123456789abcdef0123456789abcdef", id);
  }

  [Fact]
  public void ExtractClientId_NotFound_ReturnsNull() {
    var content = "console.log('hello');";
    var id = SoundCloudDiscovery.ExtractClientId(content);
    Assert.Null(id);
  }
}