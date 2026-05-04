using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

public record SubsonicStatusResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("error")] SubsonicError? Error
);