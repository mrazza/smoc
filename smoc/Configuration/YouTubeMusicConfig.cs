using Terminal.Gui.Configuration;

namespace Smoc.Configuration;

/// <summary>
/// Configuration for YouTube Music.
/// </summary>
public static class YouTubeMusicConfig {

  /// <summary>
  /// Gets or sets the player ID for the YouTube Music player.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static string? PlayerId { get; set; } = null;
}
