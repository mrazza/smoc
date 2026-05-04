
using Terminal.Gui.Configuration;

namespace Smoc.Configuration;

/// <summary>
/// Configuration for Subsonic.
/// </summary>
public static class SubsonicConfig {
  /// <summary>
  /// Gets or sets the Subsonic server host.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static string? ServerHost { get; set; } = null;

  /// <summary>
  /// Gets or sets the Subsonic server port.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static int ServerPort { get; set; } = 80;

  /// <summary>
  /// Gets or sets the Subsonic server scheme.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static string ServerScheme { get; set; } = "http";

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
