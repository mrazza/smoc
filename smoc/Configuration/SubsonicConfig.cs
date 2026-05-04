
using Terminal.Gui.Configuration;

namespace Smoc.Configuration;

/// <summary>
/// Configuration for Subsonic.
/// </summary>
public static class SubsonicConfig {
  /// <summary>
  /// Gets or sets the Subsonic server URL.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static string? ServerUrl { get; set; } = null;

  /// <summary>
  /// Gets or sets the Subsonic username.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static string? Username { get; set; } = null;

  /// <summary>
  /// Gets or sets the Subsonic password.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static string? Password { get; set; } = null;

  /// <summary>
  /// Gets or sets whether to use a token instead of a plaintext password.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static bool UseToken { get; set; } = true;
}
