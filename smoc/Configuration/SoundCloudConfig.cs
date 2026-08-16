namespace Smoc.Configuration;

/// <summary>
/// Configuration for SoundCloud.
/// </summary>
public class SoundCloudConfig {
  /// <summary>
  /// Gets or sets the SoundCloud client ID.
  /// </summary>
  public string? ClientId { get; set; } = null;

  /// <summary>
  /// Gets or sets the SoundCloud authentication token.
  /// </summary>
  public string? AuthToken { get; set; } = null;

  /// <summary>
  /// The static facade instance containing the current effective values.
  /// </summary>
  public static SoundCloudConfig Defaults { get; set; } = new();
}
