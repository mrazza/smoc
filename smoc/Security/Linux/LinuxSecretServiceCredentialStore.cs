namespace Smoc.Security.Linux;

/// <summary>
/// An <see cref="ICredentialStore"/> that uses the Linux FreeDesktop Secret Service over D-Bus (GNOME Keyring / KWallet).
/// </summary>
public sealed class LinuxSecretServiceCredentialStore : ICredentialStore {
  private readonly ISecretServiceAdapter _adapter;

  /// <summary>
  /// Initializes a new instance of <see cref="LinuxSecretServiceCredentialStore"/> using the default D-Bus adapter.
  /// </summary>
  public LinuxSecretServiceCredentialStore()
      : this(new DbusSecretServiceAdapter()) {
  }

  /// <summary>
  /// Initializes a new instance of <see cref="LinuxSecretServiceCredentialStore"/> with a custom adapter for testing.
  /// </summary>
  /// <param name="adapter">The secret service adapter.</param>
  public LinuxSecretServiceCredentialStore(ISecretServiceAdapter adapter) {
    _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
  }

  /// <inheritdoc />
  public async Task<string?> GetCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);

    return await _adapter.GetSecretAsync(service, account, cancellationToken);
  }

  /// <inheritdoc />
  public async Task SetCredentialAsync(string service, string account, string secret, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);
    ArgumentNullException.ThrowIfNull(secret);

    await _adapter.SetSecretAsync(service, account, secret, cancellationToken);
  }

  /// <inheritdoc />
  public async Task DeleteCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);

    await _adapter.DeleteSecretAsync(service, account, cancellationToken);
  }

  /// <inheritdoc />
  public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    return await _adapter.IsAvailableAsync(cancellationToken);
  }
}
