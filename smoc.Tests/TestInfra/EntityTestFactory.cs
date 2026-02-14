using System.Runtime.CompilerServices;
using Smoc.Streaming;

namespace smoc.Tests.TestInfra;

public static class EntityTestFactory {
  public static Song GenerateSong(string id = "456", [CallerMemberName] string? trackName = null, string postfix = "", bool noArt = false, TimeSpan? duration = null) {
    var radiohead = new Artist("123", "Radiohead");
    var okComputer = new Album(
      "321", radiohead, "OK Computer",
      noArt ? [] : [new AlbumCover("http://url.com/thumb_small.jpg", 128, 128), new AlbumCover("http://url.com/thumb_big.jpg", 512, 512)],
      1970);
    return new Song(id, okComputer, (trackName ?? "Paranoid Android") + postfix, duration ?? TimeSpan.FromMinutes(5), 1);
  }

  public static AlbumCover GenerateAlbumCover(string url = "http://url.com/thumb.jpg", int width = 128, int height = 128) => new AlbumCover(url, width, height);
}