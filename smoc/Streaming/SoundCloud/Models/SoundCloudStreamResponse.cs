using System.Text.Json.Serialization;

namespace Smoc.Streaming.SoundCloud.Models;

/// <summary>
/// Represents a SoundCloud stream response.
/// </summary>
/// <param name="Url">The stream URL.</param>
public record SoundCloudStreamResponse(
    [property: JsonPropertyName("url")] string Url
);