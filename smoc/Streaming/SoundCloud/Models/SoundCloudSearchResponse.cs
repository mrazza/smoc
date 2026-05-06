using System.Text.Json.Serialization;

namespace Smoc.Streaming.SoundCloud.Models;

public record SoundCloudSearchResponse<T>(
    [property: JsonPropertyName("collection")] List<T> Collection,
    [property: JsonPropertyName("next_href")] string? NextHref
);