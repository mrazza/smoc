using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

public record Playlist(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("owner")] string? Owner,
    [property: JsonPropertyName("songCount")] int? SongCount,
    [property: JsonPropertyName("duration")] int? Duration
);