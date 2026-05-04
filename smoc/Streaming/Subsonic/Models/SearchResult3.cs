using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

/// <summary>
/// Represents the result of a search operation in Subsonic.
/// </summary>
/// <param name="Artists">The list of matching artists.</param>
/// <param name="Albums">The list of matching albums.</param>
/// <param name="Songs">The list of matching songs.</param>
public record SearchResult3(
    [property: JsonPropertyName("artist")] List<Artist>? Artists,
    [property: JsonPropertyName("album")] List<Album>? Albums,
    [property: JsonPropertyName("song")] List<Song>? Songs
);