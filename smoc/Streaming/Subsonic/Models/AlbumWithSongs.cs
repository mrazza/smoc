using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

/// <summary>
/// Represents an album with its associated songs in Subsonic.
/// </summary>
/// <param name="Id">The unique identifier for the album.</param>
/// <param name="Name">The name of the album.</param>
/// <param name="ArtistName">The name of the artist for the album.</param>
/// <param name="ArtistId">The unique identifier for the artist.</param>
/// <param name="Songs">The list of songs in the album.</param>
public record AlbumWithSongs(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("artist")] string ArtistName,
    [property: JsonPropertyName("artistId")] string ArtistId,
    [property: JsonPropertyName("song")] List<Song>? Songs
);