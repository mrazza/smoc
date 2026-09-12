namespace Smoc.Configuration;

/// <summary>
/// Configuration for Tidal.
/// </summary>
public class TidalConfig {
  /// <summary>
  /// Gets or sets the Tidal Client ID.
  /// </summary>
  public string? ClientId { get; set; } = null;

  /// <summary>
  /// Gets or sets the Tidal Client Secret.
  /// </summary>
  public string? ClientSecret { get; set; } = null;

  /// <summary>
  /// Gets or sets the Tidal access token.
  /// </summary>
  public string? AccessToken { get; set; } = null;

  /// <summary>
  /// Gets or sets the Tidal refresh token.
  /// </summary>
  public string? RefreshToken { get; set; } = null;

  /// <summary>
  /// Gets or sets the Tidal token expiry timestamp in epoch seconds.
  /// </summary>
  public long? TokenExpiry { get; set; } = null;

  /// <summary>
  /// Gets or sets the Tidal audio quality setting (e.g. LOW, HIGH, LOSSLESS, HI_RES).
  /// </summary>
  public string Quality { get; set; } = "LOSSLESS";

  /// <summary>
  /// Gets or sets the Tidal ISO country code.
  /// </summary>
  public string CountryCode { get; set; } = "US";

  /// <summary>
  /// The static facade instance containing the current effective values.
  /// </summary>
  public static TidalConfig Defaults { get; set; } = new();
}
