using System.Text;

namespace Smoc.Security.Windows;

/// <summary>
/// An <see cref="ICredentialStore"/> that uses Windows Credential Manager (<c>advapi32.dll</c>) for credentials under 2 KB,
/// and DPAPI encrypted files in <c>%APPDATA%\smoc\credentials</c> for large payloads (e.g., YouTube Music cookies).
/// </summary>
public sealed class WindowsCredentialManagerCredentialStore : ICredentialStore {
  /// <summary>
  /// Maximum byte threshold before routing credentials to DPAPI-encrypted file storage to avoid
  /// Windows Credential Manager's hard 2,560-byte blob limit.
  /// </summary>
  public const int CredentialManagerBlobThresholdBytes = 2048;

  private readonly IWindowsCredentialAdapter _adapter;

  /// <summary>
  /// Initializes a new instance of <see cref="WindowsCredentialManagerCredentialStore"/> using native Windows APIs.
  /// </summary>
  public WindowsCredentialManagerCredentialStore() {
    if (!OperatingSystem.IsWindows()) {
      throw new PlatformNotSupportedException("Windows Credential Manager is only supported on Windows.");
    }

    _adapter = new NativeWindowsCredentialAdapter();
  }

  /// <summary>
  /// Initializes a new instance of <see cref="WindowsCredentialManagerCredentialStore"/> with a custom adapter for testing.
  /// </summary>
  /// <param name="adapter">The Windows credential adapter.</param>
  internal WindowsCredentialManagerCredentialStore(IWindowsCredentialAdapter adapter) {
    _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
  }

  /// <inheritdoc />
  public async Task<string?> GetCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);
    cancellationToken.ThrowIfCancellationRequested();

    if (!_adapter.IsSupportedPlatform) {
      return null;
    }

    string target = FormatTarget(service, account);

    // 1. Check if a DPAPI encrypted payload exists for large payloads
    if (_adapter.HasDpapiSecret(target)) {
      return await _adapter.ReadDpapiSecretAsync(target, cancellationToken);
    }

    // 2. Query Windows Credential Manager
    if (_adapter.ReadCredential(target, out string? secret)) {
      return secret;
    }

    return null;
  }

  /// <inheritdoc />
  public async Task SetCredentialAsync(string service, string account, string secret, CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);
    ArgumentNullException.ThrowIfNull(secret);
    cancellationToken.ThrowIfCancellationRequested();

    if (!_adapter.IsSupportedPlatform) {
      throw new PlatformNotSupportedException("Windows Credential Manager is only supported on Windows.");
    }

    byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
    string target = FormatTarget(service, account);

    if (secretBytes.Length > CredentialManagerBlobThresholdBytes) {
      // Store using DPAPI
      await _adapter.WriteDpapiSecretAsync(target, secret, cancellationToken);

      // Clean up any stale small credential in Credential Manager
      _adapter.DeleteCredential(target);
    } else {
      // Store in Windows Credential Manager
      bool success = _adapter.WriteCredential(target, account, secret);
      if (!success) {
        // If Credential Manager failed (e.g. system limit), fallback to DPAPI
        await _adapter.WriteDpapiSecretAsync(target, secret, cancellationToken);
      } else {
        // Clean up any stale DPAPI payload
        _adapter.DeleteDpapiSecret(target);
      }
    }
  }

  /// <inheritdoc />
  public Task DeleteCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);
    cancellationToken.ThrowIfCancellationRequested();

    if (!_adapter.IsSupportedPlatform) {
      return Task.CompletedTask;
    }

    string target = FormatTarget(service, account);
    _adapter.DeleteCredential(target);
    _adapter.DeleteDpapiSecret(target);
    return Task.CompletedTask;
  }

  /// <inheritdoc />
  public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    return Task.FromResult(_adapter.IsSupportedPlatform);
  }

  private static string FormatTarget(string service, string account) => $"{service}:{account}";
}
