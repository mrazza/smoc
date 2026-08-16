namespace Smoc.Configuration;

/// <summary>
/// Configuration for Subsonic.
/// </summary>
public class SubsonicConfig {
  /// <summary>
  /// Gets or sets the Subsonic server host.
  /// </summary>
  public string? ServerHost { get; set; } = null;

  /// <summary>
  /// Gets or sets the Subsonic server port.
  /// </summary>
  public int ServerPort { get; set; } = 80;

  /// <summary>
  /// Gets or sets the Subsonic server scheme.
  /// </summary>
  public string ServerScheme { get; set; } = "http";

  /// <summary>
  /// Gets or sets the Subsonic username.
  /// </summary>
  public string? Username { get; set; } = null;

  /// <summary>
  /// Gets or sets the Subsonic password.
  /// </summary>
  public string? Password { get; set; } = null;

  /// <summary>
  /// Gets or sets whether to use a token instead of a plaintext password.
  /// </summary>
  public bool UseToken { get; set; } = true;

  /// <summary>
  /// The static facade instance containing the current effective values.
  /// </summary>
  public static SubsonicConfig Defaults { get; set; } = new();
}
