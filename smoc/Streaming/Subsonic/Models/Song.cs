using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

public record Song(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("album")] string? AlbumName,
    [property: JsonPropertyName("artist")] string? ArtistName,
    [property: JsonPropertyName("artistId")] string? ArtistId,
    [property: JsonPropertyName("albumId")] string? AlbumId,
    [property: JsonPropertyName("duration")] int? Duration,
    [property: JsonPropertyName("track")] int? Track,
    [property: JsonPropertyName("coverArt")] string? CoverArt
);