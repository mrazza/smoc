using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

public record Artist(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("albumCount")] int? AlbumCount
);