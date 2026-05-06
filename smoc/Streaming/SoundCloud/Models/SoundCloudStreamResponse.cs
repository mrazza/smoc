using System.Text.Json.Serialization;

namespace Smoc.Streaming.SoundCloud.Models;

public record SoundCloudStreamResponse(
    [property: JsonPropertyName("url")] string Url
);