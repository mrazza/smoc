using System.Text.Json.Serialization;

namespace Smoc.Streaming.SoundCloud.Models;

/// <summary>
/// Represents a SoundCloud search response.
/// </summary>
/// <typeparam name="T">The type of the collection items.</typeparam>
/// <param name="Collection">The collection of items.</param>
/// <param name="NextHref">The URL for the next page of results.</param>
public record SoundCloudSearchResponse<T>(
    [property: JsonPropertyName("collection")] List<T> Collection,
    [property: JsonPropertyName("next_href")] string? NextHref
);