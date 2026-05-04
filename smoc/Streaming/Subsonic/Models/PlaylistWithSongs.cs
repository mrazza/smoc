using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

public record PlaylistWithSongs(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("entry")] List<Song>? Songs
);