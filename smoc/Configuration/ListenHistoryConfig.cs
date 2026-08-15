using System.Globalization;
using Microsoft.Extensions.Configuration;

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
  public static bool Enabled { get; set; } = true;

  /// <summary>
  /// Gets or sets the minimum position in seconds for a song to be considered listened to.
  /// </summary>
  public static int MinimumPositionSeconds { get; set; } = 30;

  /// <summary>
  /// Gets or sets the minimum fraction of a song for it to be considered listened to.
  /// </summary>
  public static double MinimumFraction { get; set; } = 0.5;

  /// <summary>
  /// Binds configuration settings from the specified <see cref="IConfiguration"/>.
  /// </summary>
  /// <param name="config">The configuration source.</param>
  public static void Bind(IConfiguration config) {
    var section = config.GetSection("ListenHistoryConfig");
    if (section.Exists()) {
      if (bool.TryParse(section["Enabled"], out var enabled)) Enabled = enabled;
      if (int.TryParse(section["MinimumPositionSeconds"], out var minSec)) MinimumPositionSeconds = minSec;
      if (double.TryParse(section["MinimumFraction"], CultureInfo.InvariantCulture, out var minFrac)) MinimumFraction = minFrac;
    }
    if (bool.TryParse(config["ListenHistoryConfig.Enabled"], out var flatEnabled)) Enabled = flatEnabled;
    if (int.TryParse(config["ListenHistoryConfig.MinimumPositionSeconds"], out var flatMinSec)) MinimumPositionSeconds = flatMinSec;
    if (double.TryParse(config["ListenHistoryConfig.MinimumFraction"], CultureInfo.InvariantCulture, out var flatMinFrac)) MinimumFraction = flatMinFrac;
  }
}
