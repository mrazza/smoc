using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

/// <summary>
/// Represents a song in Subsonic.
/// </summary>
/// <param name="Id">The unique identifier for the song.</param>
/// <param name="Title">The title of the song.</param>
/// <param name="AlbumName">The name of the album containing the song.</param>
/// <param name="ArtistName">The name of the artist for the song.</param>
/// <param name="ArtistId">The unique identifier for the artist.</param>
/// <param name="AlbumId">The unique identifier for the album.</param>
/// <param name="Duration">The duration of the song in seconds.</param>
/// <param name="Track">The track number of the song.</param>
/// <param name="CoverArt">The ID of the cover art for the song.</param>
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