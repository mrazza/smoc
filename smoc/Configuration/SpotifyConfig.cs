namespace Smoc.Configuration;

/// <summary>
/// Configuration for Spotify.
/// </summary>
public class SpotifyConfig {
  /// <summary>
  /// Gets or sets the Spotify Client ID.
  /// </summary>
  public string? ClientId { get; set; } = null;

  /// <summary>
  /// Gets or sets the Spotify Client Secret.
  /// </summary>
  public string? ClientSecret { get; set; } = null;

  /// <summary>
  /// Gets or sets the Spotify username.
  /// </summary>
  public string? Username { get; set; } = null;

  /// <summary>
  /// Gets or sets the Spotify password.
  /// </summary>
  public string? Password { get; set; } = null;

  /// <summary>
  /// Gets or sets the Spotify cache directory.
  /// </summary>
  public string? CacheDirectory { get; set; } = null;

  /// <summary>
  /// The static facade instance containing the current effective values.
  /// </summary>
  public static SpotifyConfig Defaults { get; set; } = new();
}
