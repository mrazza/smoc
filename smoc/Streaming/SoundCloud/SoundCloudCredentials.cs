namespace Smoc.Streaming.SoundCloud;

/// <summary>
/// Connection and authentication credentials for SoundCloud.
/// </summary>
/// <param name="ClientId">The discovered or configured SoundCloud client ID.</param>
/// <param name="AuthToken">Optional user OAuth authentication token.</param>
public record SoundCloudCredentials(string? ClientId, string? AuthToken);
