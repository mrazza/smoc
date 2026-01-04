namespace Smoc.Streaming;

public sealed record Song(string Id, Album Album, int TrackNumber, string Title, TimeSpan Duration) : Entity(Id)
{
    public Artist Artist => Album.Artist;
}
