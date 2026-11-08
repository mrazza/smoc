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

