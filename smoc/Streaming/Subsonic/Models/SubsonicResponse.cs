using System.Text.Json.Serialization;

namespace Smoc.Streaming.Subsonic.Models;

public record SubsonicResponse<T>(
    [property: JsonPropertyName("subsonic-response")] T Response
);