using System.Runtime.CompilerServices;
using Smoc.Streaming;

namespace smoc.Tests.TestInfra;

public static class EntityTestFactory {
  public static Song GenerateSong(string id = "456", [CallerMemberName] string? trackName = null, string postfix = "", bool noArt = false) {
    var radiohead = new Artist("123", "Radiohead");
    var okComputer = new Album("321", radiohead, "OK Computer", 1970, noArt ? null : "http://url.com/thumb.jpg");
    return new Song(id, okComputer, (trackName ?? "Paranoid Android") + postfix, TimeSpan.FromMinutes(5), 1);
  }
}