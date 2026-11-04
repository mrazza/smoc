namespace Smoc.Security.MacOs;

/// <summary>
/// Abstraction over macOS Keychain operations for testability.
/// </summary>
public interface IMacOsKeychainAdapter {
  /// <summary>
  /// Gets whether macOS Keychain is supported and available on the current operating system.
  /// </summary>
  bool IsSupportedPlatform { get; }

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
