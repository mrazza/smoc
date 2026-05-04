using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

/// <summary>
/// Represents a playlist in Subsonic.
/// </summary>
/// <param name="Id">The unique identifier for the playlist.</param>
/// <param name="Name">The name of the playlist.</param>
/// <param name="Owner">The owner of the playlist.</param>
/// <param name="SongCount">The number of songs in the playlist.</param>
/// <param name="Duration">The total duration of the playlist in seconds.</param>
public record Playlist(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("owner")] string? Owner,
    [property: JsonPropertyName("songCount")] int? SongCount,
    [property: JsonPropertyName("duration")] int? Duration
);