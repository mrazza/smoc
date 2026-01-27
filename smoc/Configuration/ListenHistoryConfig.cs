using System.Text.Json.Serialization;
using Terminal.Gui.Configuration;

namespace Smoc.Configuration;

/// <summary>
/// Configuration for listen history tracking.
/// </summary>
/// <remarks>
/// One of <see cref="MinimumPositionSeconds"/> or <see cref="MinimumFraction"/> must be reached for a song to be considered listened to.
/// </remarks>
public static class ListenHistoryConfig {

  /// <summary>
  /// Gets or sets whether listen history tracking is enabled.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  [property: JsonPropertyName("enabled")]
  public static bool Enabled { get; set; } = true;

  /// <summary>
  /// Gets or sets the minimum position in seconds for a song to be considered listened to.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  [property: JsonPropertyName("minimumPositionSeconds")]
  public static int MinimumPositionSeconds { get; set; } = 30;

  /// <summary>
  /// Gets or sets the minimum fraction of a song for it to be considered listened to.
  /// </summary>
  [ConfigurationProperty(Scope = typeof(SettingsScope))]
  [property: JsonPropertyName("minimumFraction")]
  public static double MinimumFraction { get; set; } = 0.5;
}
