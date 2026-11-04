namespace Smoc.Security;

/// <summary>
/// Provides an abstraction for securely storing, retrieving, and deleting service credentials across platforms.
/// </summary>
public interface ICredentialStore {
  /// <summary>
  /// Retrieves a credential secret for the specified service and account.
  /// </summary>
  /// <param name="service">The target service identifier (e.g., <c>smoc:subsonic</c>).</param>
  /// <param name="account">The account name or key associated with the credential.</param>
  /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
  /// <returns>The stored secret, or <c>null</c> if not found.</returns>
  Task<string?> GetCredentialAsync(string service, string account, CancellationToken cancellationToken = default);

  /// <summary>
  /// Stores or updates a credential secret for the specified service and account.
  /// </summary>
  /// <param name="service">The target service identifier.</param>
  /// <param name="account">The account name or key associated with the credential.</param>
  /// <param name="secret">The secret value to store.</param>
  /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
  Task SetCredentialAsync(string service, string account, string secret, CancellationToken cancellationToken = default);

  /// <summary>
  /// Deletes a stored credential for the specified service and account if present.
  /// </summary>
  /// <param name="service">The target service identifier.</param>
  /// <param name="account">The account name or key associated with the credential.</param>
  /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
  Task DeleteCredentialAsync(string service, string account, CancellationToken cancellationToken = default);

  /// <summary>
  /// Checks whether the underlying credential store provider is available in the current runtime environment.
  /// </summary>
  /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
  /// <returns><c>true</c> if the store is available; otherwise, <c>false</c>.</returns>
  Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
