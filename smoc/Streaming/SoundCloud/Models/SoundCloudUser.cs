using System.Text.Json.Serialization;

namespace Smoc.Streaming.SoundCloud.Models;

/// <summary>
/// Represents a SoundCloud user.
/// </summary>
/// <param name="Id">The user ID.</param>
/// <param name="Username">The username.</param>
/// <param name="AvatarUrl">The user avatar URL.</param>
public record SoundCloudUser(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("avatar_url")] string AvatarUrl
);