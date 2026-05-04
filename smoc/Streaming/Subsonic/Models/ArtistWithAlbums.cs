using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

public record ArtistWithAlbums(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("album")] List<Album>? Albums
);