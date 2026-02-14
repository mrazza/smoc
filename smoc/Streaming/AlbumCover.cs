namespace Smoc.Streaming;

/// <summary>
/// Represents a music album cover.
/// </summary>
/// <param name="Url">The URL of the album cover.</param>
/// <param name="Width">The width of the album cover in pixels.</param>
/// <param name="Height">The height of the album cover in pixels.</param>
public sealed record AlbumCover(string Url, int Width, int Height);
