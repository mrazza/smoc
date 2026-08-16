namespace Smoc.Configuration;

/// <summary>
/// Configuration for YouTube Music.
/// </summary>
public class YouTubeMusicConfig {

  /// <summary>
  /// Gets or sets the player ID for the YouTube Music player.
  /// </summary>
  public string? PlayerId { get; set; } = null;

  /// <summary>
  /// The static facade instance containing the current effective values.
  /// </summary>
  public static YouTubeMusicConfig Defaults { get; set; } = new();
}
