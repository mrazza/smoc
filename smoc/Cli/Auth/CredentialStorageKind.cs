namespace Smoc.Cli.Auth;

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

