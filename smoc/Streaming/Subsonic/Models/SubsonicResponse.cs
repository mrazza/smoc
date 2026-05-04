using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

/// <summary>
/// Represents the standard Subsonic API response wrapper.
/// </summary>
/// <typeparam name="T">The type of the response payload.</typeparam>
/// <param name="Response">The response payload.</param>
public record SubsonicResponse<T>(
    [property: JsonPropertyName("subsonic-response")] T Response
);