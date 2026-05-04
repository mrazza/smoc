using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

/// <summary>
/// Represents an album in Subsonic.
/// </summary>
/// <param name="Id">The unique identifier for the album.</param>
/// <param name="Name">The name of the album.</param>
/// <param name="ArtistName">The name of the artist for the album.</param>
/// <param name="ArtistId">The unique identifier for the artist.</param>
/// <param name="SongCount">The number of songs in the album.</param>
/// <param name="Duration">The total duration of the album in seconds.</param>
/// <param name="CoverArt">The ID of the cover art for the album.</param>
public record Album(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("artist")] string? ArtistName,
    [property: JsonPropertyName("artistId")] string? ArtistId,
    [property: JsonPropertyName("songCount")] int? SongCount,
    [property: JsonPropertyName("duration")] int? Duration,
    [property: JsonPropertyName("coverArt")] string? CoverArt
);