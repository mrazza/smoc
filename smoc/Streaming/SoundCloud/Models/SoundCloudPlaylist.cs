using System.Text.Json.Serialization;

namespace Smoc.Streaming.SoundCloud.Models;

/// <summary>
/// Represents a SoundCloud playlist.
/// </summary>
/// <param name="Id">The playlist ID.</param>
/// <param name="Title">The playlist title.</param>
/// <param name="Tracks">The tracks in the playlist.</param>
public record SoundCloudPlaylist(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("tracks")] List<SoundCloudTrack> Tracks
);