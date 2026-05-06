using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

public record TidalPlaylist(
    [property: JsonPropertyName("uuid")] string Uuid,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description = null
);