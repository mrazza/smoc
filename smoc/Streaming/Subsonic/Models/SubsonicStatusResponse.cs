using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

/// <summary>
/// Represents a basic Subsonic API response containing status information.
/// </summary>
/// <param name="Status">The status of the response (e.g., "ok" or "failed").</param>
/// <param name="Version">The version of the Subsonic API.</param>
/// <param name="Error">Optional error information if the status is "failed".</param>
public record SubsonicStatusResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("error")] SubsonicError? Error
);