namespace Smoc.Streaming;

/// <summary>
/// Represents a music album.
/// </summary>
/// <param name="Id">The unique identifier of the album.</param>
/// <param name="Artist">The artist who created the album.</param>
/// <param name="Name">The name of the album.</param>
/// <param name="Covers">The album covers.</param>
/// <param name="ReleaseYear">The year the album was released.</param>
public sealed record Album(string Id, Artist Artist, string Name, IEnumerable<AlbumCover> Covers, int? ReleaseYear = null) : Entity(Id);
