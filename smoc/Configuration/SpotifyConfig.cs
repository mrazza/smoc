using Terminal.Gui.Configuration;

namespace Smoc.Configuration;

/// <summary>
/// Configuration for Spotify.
/// </summary>
public static class SpotifyConfig {
  /// <summary>
  /// Gets or sets the Spotify username.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static string? Username { get; set; } = null;

  /// <summary>
  /// Gets or sets the Spotify password.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static string? Password { get; set; } = null;

  /// <summary>
  /// Gets or sets the Spotify Client ID.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static string? ClientId { get; set; } = null;

  /// <summary>
  /// Gets or sets the Spotify Client Secret.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static string? ClientSecret { get; set; } = null;

  /// <summary>
  /// Gets or sets the Spotify cache directory.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static string? CacheDirectory { get; set; } = null;
}