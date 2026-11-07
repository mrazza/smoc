namespace Smoc.Security.Linux;

/// <summary>
/// Abstraction over FreeDesktop Secret Service communications for unit testability.
/// </summary>
internal interface ISecretServiceAdapter {
  /// <summary>
  /// Checks whether the Secret Service daemon is available on the current session bus.
  /// </summary>
  Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

  /// <summary>
  /// Retrieves a secret stored with the given service and account attributes.
  /// </summary>
  Task<string?> GetSecretAsync(string service, string account, CancellationToken cancellationToken);

  /// <summary>
  /// Stores a secret with the given service and account attributes.
  /// </summary>
  Task SetSecretAsync(string service, string account, string secret, CancellationToken cancellationToken);

  /// <summary>
  /// Deletes any secret stored with the given service and account attributes.
  /// </summary>
  Task DeleteSecretAsync(string service, string account, CancellationToken cancellationToken);
}
