namespace Smoc.Streaming;

/// <summary>
/// Represents a music album.
/// </summary>
/// <param name="Id">The unique identifier of the album.</param>
/// <param name="Artist">The artist who created the album.</param>
/// <param name="Name">The name of the album.</param>
/// <param name="ReleaseYear">The year the album was released.</param>
/// <param name="SmallThumbnailUrl">Small album art URL.</param>
/// <param name="LargeThumbnailUrl">Large album art URL.</param>
public sealed record Album(string Id, Artist Artist, string Name, int? ReleaseYear = null, string? SmallThumbnailUrl = null, string? LargeThumbnailUrl = null) : Entity(Id);
