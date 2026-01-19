namespace Smoc.Streaming;

/// <summary>
/// Represents a music artist.
/// </summary>
/// <param name="Id">The unique identifier of the artist.</param>
/// <param name="Name">The name of the artist.</param>
public sealed record Artist(string Id, string Name) : Entity(Id);
