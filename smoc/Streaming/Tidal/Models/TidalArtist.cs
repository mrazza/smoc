using System.Text.Json.Serialization;

namespace Smoc.Streaming.Tidal.Models;

public record TidalArtist(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("picture")] string? Picture = null
);