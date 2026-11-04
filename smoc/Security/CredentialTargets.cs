namespace Smoc.Security;

/// <summary>
/// Defines standard service names and account keys used for credential storage across SMoC services.
/// </summary>
public static class CredentialTargets {
  /// <summary>
  /// Service identifier for Subsonic credentials.
  /// </summary>
  public const string Subsonic = "smoc:subsonic";

  /// <summary>
  /// Service identifier for SoundCloud credentials.
  /// </summary>
  public const string SoundCloud = "smoc:soundcloud";

  /// <summary>
  /// Service identifier for YouTube Music credentials.
  /// </summary>
  public const string YouTubeMusic = "smoc:youtubemusic";

  /// <summary>
  /// Service identifier for Spotify credentials.
  /// </summary>
  public const string Spotify = "smoc:spotify";

  /// <summary>
  /// Service identifier for Tidal credentials.
  /// </summary>
  public const string Tidal = "smoc:tidal";

  /// <summary>
  /// Account key for YouTube Music session cookies.
  /// </summary>
  public const string AccountCookies = "cookies";

  /// <summary>
  /// Account key for YouTube Music visitor/PO tokens.
  /// </summary>
  public const string AccountTokens = "tokens";

  /// <summary>
  /// Account key for OAuth authentication tokens (e.g. SoundCloud).
  /// </summary>
  public const string AccountAuthToken = "auth_token";

  /// <summary>
  /// Account key for OAuth client ID.
  /// </summary>
  public const string AccountClientId = "client_id";
}
