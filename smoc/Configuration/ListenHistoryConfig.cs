namespace Smoc.Configuration;

/// <summary>
/// Configuration for listen history tracking.
/// </summary>
/// <remarks>
/// One of <see cref="MinimumPositionSeconds"/> or <see cref="MinimumFraction"/> must be reached for a song to be considered listened to.
/// </remarks>
public class ListenHistoryConfig {

  /// <summary>
  /// Gets or sets whether listen history tracking is enabled.
  /// </summary>
  public bool Enabled { get; set; } = true;

  /// <summary>
  /// Gets or sets the minimum position in seconds for a song to be considered listened to.
  /// </summary>
  public int MinimumPositionSeconds { get; set; } = 30;

  /// <summary>
  /// Gets or sets the minimum fraction of a song for it to be considered listened to.
  /// </summary>
  public double MinimumFraction { get; set; } = 0.5;

  /// <summary>
  /// The static facade instance containing the current effective values.
  /// </summary>
  public static ListenHistoryConfig Defaults { get; set; } = new();
}
