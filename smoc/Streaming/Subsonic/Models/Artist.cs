using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

/// <summary>
/// Represents an artist in Subsonic.
/// </summary>
/// <param name="Id">The unique identifier for the artist.</param>
/// <param name="Name">The name of the artist.</param>
/// <param name="AlbumCount">The number of albums by this artist.</param>
public record Artist(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("albumCount")] int? AlbumCount
);