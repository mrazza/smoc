namespace Smoc.Security.MacOs;

/// <summary>
/// Abstraction over macOS Keychain operations for testability.
/// </summary>
internal interface IMacOsKeychainAdapter {
  /// <summary>
  /// Retrieves a secret stored in macOS Keychain for the given service and account.
  /// </summary>
  Task<string?> GetSecretAsync(string service, string account, CancellationToken cancellationToken);

  /// <summary>
  /// Stores a secret in macOS Keychain for the given service and account.
  /// </summary>
  Task SetSecretAsync(string service, string account, string secret, CancellationToken cancellationToken);

  /// <summary>
  /// Deletes a secret from macOS Keychain for the given service and account.
  /// </summary>
  Task DeleteSecretAsync(string service, string account, CancellationToken cancellationToken);
}
