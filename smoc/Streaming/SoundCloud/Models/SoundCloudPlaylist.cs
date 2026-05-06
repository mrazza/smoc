using System.Text.Json.Serialization;

namespace Smoc.Streaming.SoundCloud.Models;

public record SoundCloudPlaylist(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("tracks")] List<SoundCloudTrack> Tracks
);