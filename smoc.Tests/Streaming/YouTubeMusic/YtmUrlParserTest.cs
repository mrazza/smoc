using Smoc.Streaming;
using Smoc.Streaming.YouTubeMusic;

namespace smoc.Tests.Streaming.YouTubeMusic;

public class YtmUrlParserTest {
  [Fact]
  public void Parse_ValidSongUrl_ReturnsCorrectIdAndType() {
    var url = "https://music.youtube.com/watch?v=dQw4w9WgXcQ";
    var (type, id) = YtmUrlParser.ParseUrl(url);
    Assert.Equal(typeof(Song), type);
    Assert.Equal("dQw4w9WgXcQ", id);
  }

  [Fact]
  public void Parse_ValidPlaylistUrl_ReturnsCorrectIdAndType() {
    var url = "https://music.youtube.com/playlist?list=PLMC9KNQr(_)";
    var (type, id) = YtmUrlParser.ParseUrl(url);
    Assert.Equal(typeof(Playlist), type);
    Assert.Equal("PLMC9KNQr(_)", id);
  }

  [Fact]
  public void Parse_UnknownDomain_ThrowsArgumentException() {
    var url = "https://www.google.com";
    Assert.Throws<ArgumentException>(() => YtmUrlParser.ParseUrl(url));
  }

  [Fact]
  public void Parse_UnknownPath_ThrowsArgumentException() {
    var url = "https://music.youtube.com/unknown";
    Assert.Throws<ArgumentException>(() => YtmUrlParser.ParseUrl(url));
  }

  [Fact]
  public void Parse_SongUrlWithUnknownQuery_ThrowsArgumentException() {
    var url = "https://music.youtube.com/watch?q=unknown";
    Assert.Throws<ArgumentException>(() => YtmUrlParser.ParseUrl(url));
  }

  [Fact]
  public void Parse_PlaylistUrlWithUnknownQuery_ThrowsArgumentException() {
    var url = "https://music.youtube.com/playlist?q=unknown";
    Assert.Throws<ArgumentException>(() => YtmUrlParser.ParseUrl(url));
  }
}