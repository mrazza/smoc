using Terminal.Gui.Configuration;

namespace Smoc.Configuration;

/// <summary>
/// Configuration for SoundCloud.
/// </summary>
public static class SoundCloudConfig {
  /// <summary>
  /// Gets or sets the SoundCloud client ID.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static string? ClientId { get; set; } = null;

  /// <summary>
  /// Gets or sets the SoundCloud authentication token.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static string? AuthToken { get; set; } = null;
}