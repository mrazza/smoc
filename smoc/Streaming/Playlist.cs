namespace Smoc.Streaming;

/// <summary>
/// Represents a music playlist.
/// </summary>
/// <param name="Id">The unique identifier of the playlist.</param>
/// <param name="Name">The name of the playlist.</param>
public record Playlist(string Id, string Name) : Entity(Id);
