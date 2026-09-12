using Smoc.Streaming.Tidal;
using Xunit;

namespace smoc.Tests.Streaming.Tidal;

public class TidalUrlParserTest {
  [Theory]
  [InlineData("https://tidal.com/browse/track/123456", "track", "123456")]
  [InlineData("https://tidal.com/track/987654?si=abc123xyz", "track", "987654")]
  [InlineData("https://listen.tidal.com/track/112233/", "track", "112233")]
  [InlineData("https://tidal.com/browse/album/554433", "album", "554433")]
  [InlineData("https://tidal.com/album/443322/?si=queryParam", "album", "443322")]
  [InlineData("https://listen.tidal.com/album/998877", "album", "998877")]
  [InlineData("https://tidal.com/playlist/7e452778-d5a2-4a0e-953e-b49d6350f0ec", "playlist", "7e452778-d5a2-4a0e-953e-b49d6350f0ec")]
  [InlineData("https://tidal.com/browse/playlist/7e452778-d5a2-4a0e-953e-b49d6350f0ec?si=xyz", "playlist", "7e452778-d5a2-4a0e-953e-b49d6350f0ec")]
  [InlineData("https://listen.tidal.com/playlist/7e452778-d5a2-4a0e-953e-b49d6350f0ec/", "playlist", "7e452778-d5a2-4a0e-953e-b49d6350f0ec")]
  public void TryParseUrl_ValidUrls_ParsesSuccessfully(string url, string expectedType, string expectedId) {
    var success = TidalUrlParser.TryParseUrl(url, out var type, out var id);

    Assert.True(success);
    Assert.Equal(expectedType, type);
    Assert.Equal(expectedId, id);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("https://tidal.com/")]
  [InlineData("https://tidal.com/browse/artist/12345")]
  [InlineData("https://example.com/other/path")]
  public void TryParseUrl_InvalidUrls_ReturnsFalse(string url) {
    var success = TidalUrlParser.TryParseUrl(url, out var type, out var id);

    Assert.False(success);
    Assert.Empty(type);
    Assert.Empty(id);
  }
}
