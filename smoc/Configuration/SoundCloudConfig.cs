using Microsoft.Extensions.Configuration;

namespace Smoc.Configuration;

/// <summary>
/// Configuration for SoundCloud.
/// </summary>
public static class SoundCloudConfig {
  /// <summary>
  /// Gets or sets the SoundCloud client ID.
  /// </summary>
  public static string? ClientId { get; set; } = null;

  /// <summary>
  /// Gets or sets the SoundCloud authentication token.
  /// </summary>
  public static string? AuthToken { get; set; } = null;

  /// <summary>
  /// Binds configuration settings from the specified <see cref="IConfiguration"/>.
  /// </summary>
  /// <param name="config">The configuration source.</param>
  public static void Bind(IConfiguration config) {
    var section = config.GetSection("SoundCloudConfig");
    if (section.Exists()) {
      if (section["ClientId"] is { } clientId) ClientId = clientId;
      if (section["AuthToken"] is { } authToken) AuthToken = authToken;
    }
    if (config["SoundCloudConfig.ClientId"] is { } flatClientId) ClientId = flatClientId;
    if (config["SoundCloudConfig.AuthToken"] is { } flatAuthToken) AuthToken = flatAuthToken;
  }
}
