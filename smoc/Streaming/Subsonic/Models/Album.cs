using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

public record Album(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("artist")] string? ArtistName,
    [property: JsonPropertyName("artistId")] string? ArtistId,
    [property: JsonPropertyName("songCount")] int? SongCount,
    [property: JsonPropertyName("duration")] int? Duration,
    [property: JsonPropertyName("coverArt")] string? CoverArt
);