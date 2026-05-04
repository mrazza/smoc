using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

public record SearchResult3(
    [property: JsonPropertyName("artist")] List<Artist>? Artists,
    [property: JsonPropertyName("album")] List<Album>? Albums,
    [property: JsonPropertyName("song")] List<Song>? Songs
);