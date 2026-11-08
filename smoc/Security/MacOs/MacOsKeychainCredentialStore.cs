namespace Smoc.Security.MacOs;

/// <summary>
/// An <see cref="ICredentialStore"/> that uses Apple's native macOS Keychain.
/// </summary>
public sealed class MacOsKeychainCredentialStore : ICredentialStore {
  private readonly IMacOsKeychainAdapter _adapter;

  /// <summary>
  /// Initializes a new instance of <see cref="MacOsKeychainCredentialStore"/> using the native macOS adapter.
  /// </summary>
  /// <exception cref="PlatformNotSupportedException">Thrown when not running on macOS.</exception>
  public MacOsKeychainCredentialStore() {
    if (!OperatingSystem.IsMacOS()) {
      throw new PlatformNotSupportedException("macOS Keychain is only supported on macOS.");
    }

    _adapter = new NativeMacOsKeychainAdapter();
  }

  /// <summary>
  /// Initializes a new instance of <see cref="MacOsKeychainCredentialStore"/> with a custom adapter for testing.
  /// </summary>
  /// <param name="adapter">The macOS Keychain adapter.</param>
  internal MacOsKeychainCredentialStore(IMacOsKeychainAdapter adapter) {
    _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
  }

  /// <inheritdoc />
  public async Task<string?> GetCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);
    cancellationToken.ThrowIfCancellationRequested();

    return await _adapter.GetSecretAsync(service, account, cancellationToken);
  }

  /// <inheritdoc />
  public async Task SetCredentialAsync(string service, string account, string secret, CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);
    ArgumentNullException.ThrowIfNull(secret);
    cancellationToken.ThrowIfCancellationRequested();

    await _adapter.SetSecretAsync(service, account, secret, cancellationToken);
  }

  /// <inheritdoc />
  public async Task DeleteCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);
    cancellationToken.ThrowIfCancellationRequested();

    await _adapter.DeleteSecretAsync(service, account, cancellationToken);
  }

  /// <inheritdoc />
  public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    return Task.FromResult(true);
  }
}
