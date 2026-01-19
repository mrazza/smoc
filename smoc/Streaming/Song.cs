namespace Smoc.Streaming;

public sealed record Song(string Id, Album Album, string Title, TimeSpan Duration, int? TrackNumber = null) : Entity(Id) {
  public Artist Artist => Album.Artist;
}
