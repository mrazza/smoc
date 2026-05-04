using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

/// <summary>
/// Represents an error returned by the Subsonic API.
/// </summary>
/// <param name="Code">The error code.</param>
/// <param name="Message">The error message.</param>
public record SubsonicError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message
);