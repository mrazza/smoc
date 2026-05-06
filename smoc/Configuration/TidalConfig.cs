using Terminal.Gui.Configuration;

namespace Smoc.Configuration;

/// <summary>
/// Configuration for Tidal.
/// </summary>
public static class TidalConfig {
  /// <summary>
  /// Gets or sets the Tidal Client ID.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static string? ClientId { get; set; } = null;

  /// <summary>
  /// Gets or sets the Tidal Client Secret.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static string? ClientSecret { get; set; } = null;

  /// <summary>
  /// Gets or sets the Tidal access token.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static string? AccessToken { get; set; } = null;

  /// <summary>
  /// Gets or sets the Tidal refresh token.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static string? RefreshToken { get; set; } = null;

  /// <summary>
  /// Gets or sets the Tidal country code.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static string CountryCode { get; set; } = "US";

  /// <summary>
  /// Gets or sets the Tidal audio quality.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  public static string AudioQuality { get; set; } = "LOSSLESS";
}