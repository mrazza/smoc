using System.Text.Json.Serialization;

namespace Smoc.Streaming.SoundCloud.Models;

public record SoundCloudUser(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("avatar_url")] string AvatarUrl
);