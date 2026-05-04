using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

/// <summary>
/// Represents a playlist with its associated songs in Subsonic.
/// </summary>
/// <param name="Id">The unique identifier for the playlist.</param>
/// <param name="Name">The name of the playlist.</param>
/// <param name="Songs">The list of songs in the playlist.</param>
public record PlaylistWithSongs(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("entry")] List<Song>? Songs
);