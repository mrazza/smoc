namespace Smoc.Security.MacOs;

/// <summary>
/// An <see cref="ICredentialStore"/> that uses Apple's native macOS Keychain.
/// </summary>
public sealed class MacOsKeychainCredentialStore : ICredentialStore {
  private readonly IMacOsKeychainAdapter _adapter;

  /// <summary>
  /// Initializes a new instance of <see cref="MacOsKeychainCredentialStore"/> using the native macOS adapter.
  /// </summary>
  public MacOsKeychainCredentialStore()
      : this(new NativeMacOsKeychainAdapter()) {
  }

  /// <summary>
  /// Initializes a new instance of <see cref="MacOsKeychainCredentialStore"/> with a custom adapter for testing.
  /// </summary>
  /// <param name="adapter">The macOS Keychain adapter.</param>
  public MacOsKeychainCredentialStore(IMacOsKeychainAdapter adapter) {
    _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
  }

  /// <inheritdoc />
  public Task<string?> GetCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);

    return _adapter.GetSecretAsync(service, account, cancellationToken);
  }

  /// <inheritdoc />
  public Task SetCredentialAsync(string service, string account, string secret, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);
    ArgumentNullException.ThrowIfNull(secret);

    return _adapter.SetSecretAsync(service, account, secret, cancellationToken);
  }

  /// <inheritdoc />
  public Task DeleteCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);

    return _adapter.DeleteSecretAsync(service, account, cancellationToken);
  }

  /// <inheritdoc />
  public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    return Task.FromResult(_adapter.IsSupportedPlatform);
  }
}
