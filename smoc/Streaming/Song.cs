namespace Smoc.Streaming;

/// <summary>
/// Represents a song.
/// </summary>
/// <param name="Id">The unique identifier of the song.</param>
/// <param name="Album">The album the song belongs to.</param>
/// <param name="Title">The title of the song.</param>
/// <param name="Duration">The duration of the song.</param>
/// <param name="TrackNumber">The track number on the album.</param>
public sealed record Song(string Id, Album Album, string Title, TimeSpan Duration, int? TrackNumber = null) : Entity(Id) {
  /// <summary>
  /// Gets the artist of the song (derived from the Album).
  /// </summary>
  public Artist Artist => Album.Artist;
}
