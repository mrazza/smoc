using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

/// <summary>
/// Represents an artist with their associated albums in Subsonic.
/// </summary>
/// <param name="Id">The unique identifier for the artist.</param>
/// <param name="Name">The name of the artist.</param>
/// <param name="Albums">The list of albums by this artist.</param>
public record ArtistWithAlbums(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("album")] List<Album>? Albums
);