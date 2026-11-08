namespace Smoc.Cli.Auth;

/// <summary>
/// Represents the authentication state of a streaming service.
/// </summary>
public enum AuthStatusState {
  /// <summary>
  /// The service has valid credentials stored in secure storage or environment variables.
  /// </summary>
  Authenticated,

  /// <summary>
  /// Session cookies are stored, but derived session tokens are missing.
  /// </summary>
  CookiesStored,

  /// <summary>
  /// Credentials are present only in legacy plaintext configuration or files.
  /// </summary>
  ConfiguredInsecure,

  /// <summary>
  /// Non-secret account configuration is present, but the secret credential is missing.
  /// </summary>
  MissingCredentials,

  /// <summary>
  /// The service has no configured credentials or account settings.
  /// </summary>
  NotConfigured,

  /// <summary>
  /// An error occurred while reading configuration or querying the credential store.
  /// </summary>
  Error
}

/// <summary>
/// Identifies where a service's credentials are currently stored.
/// </summary>
public enum CredentialStorageKind {
  /// <summary>
  /// No credentials are stored.
  /// </summary>
  None,

  /// <summary>
  /// Stored in the operating system's native keyring or credential manager.
  /// </summary>
  Keyring,

  /// <summary>
  /// Provided via process environment variables.
  /// </summary>
  Environment,

  /// <summary>
  /// Stored in plaintext inside <c>config.json</c>.
  /// </summary>
  PlaintextConfig,

  /// <summary>
  /// Stored in a plaintext file on disk (e.g., <c>cookies.txt</c>).
  /// </summary>
  PlaintextFile
}

/// <summary>
/// Identifies target services for authentication CLI operations.
/// </summary>
public enum AuthServiceTarget {
  /// <summary>
  /// Subsonic-compatible servers.
  /// </summary>
  Subsonic,

  /// <summary>
  /// SoundCloud.
  /// </summary>
  SoundCloud,

  /// <summary>
  /// YouTube Music.
  /// </summary>
  YouTubeMusic,

  /// <summary>
  /// All supported streaming services.
  /// </summary>
  All
}

/// <summary>
/// Parses service target names and aliases.
/// </summary>
public static class AuthServiceTargetParser {
  /// <summary>
  /// Attempts to parse a service name or alias into an <see cref="AuthServiceTarget"/>.
  /// </summary>
  /// <param name="serviceName">The raw service name input.</param>
  /// <param name="target">When this method returns, contains the parsed target if successful.</param>
  /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
  public static bool TryParse(string? serviceName, out AuthServiceTarget target) {
    if (!string.IsNullOrWhiteSpace(serviceName)) {
      switch (serviceName.Trim().ToLowerInvariant()) {
        case "subsonic":
          target = AuthServiceTarget.Subsonic;
          return true;
        case "soundcloud":
          target = AuthServiceTarget.SoundCloud;
          return true;
        case "youtube":
        case "youtubemusic":
        case "ytm":
          target = AuthServiceTarget.YouTubeMusic;
          return true;
        case "all":
          target = AuthServiceTarget.All;
          return true;
      }
    }

    target = default;
    return false;
  }
}

/// <summary>
/// Represents the authentication and storage status of a streaming service.
/// </summary>
/// <param name="Service">The user-friendly name of the streaming service.</param>
/// <param name="Status">The current authentication status state.</param>
/// <param name="Storage">The credential storage mechanism.</param>
/// <param name="Details">Additional context such as username, host, or cookie/token validity.</param>
public record ServiceAuthStatus(
  string Service,
  AuthStatusState Status,
  CredentialStorageKind Storage,
  string Details
);
