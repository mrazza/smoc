using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

public record SubsonicError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message
);