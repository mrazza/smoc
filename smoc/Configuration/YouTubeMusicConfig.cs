using Microsoft.Extensions.Configuration;

namespace Smoc.Configuration;

/// <summary>
/// Configuration for YouTube Music.
/// </summary>
public static class YouTubeMusicConfig {

  /// <summary>
  /// Gets or sets the player ID for the YouTube Music player.
  /// </summary>
  public static string? PlayerId { get; set; } = null;

  /// <summary>
  /// Binds configuration settings from the specified <see cref="IConfiguration"/>.
  /// </summary>
  /// <param name="config">The configuration source.</param>
  public static void Bind(IConfiguration config) {
    var section = config.GetSection("YouTubeMusicConfig");
    if (section.Exists()) {
      if (section["PlayerId"] is { } playerId) PlayerId = playerId;
    }
    if (config["YouTubeMusicConfig.PlayerId"] is { } flatPlayerId) PlayerId = flatPlayerId;
  }
}
