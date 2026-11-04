using System.Security.Cryptography;
using System.Text;

namespace Smoc.Security.Windows;

/// <summary>
/// An <see cref="ICredentialStore"/> that uses Windows Credential Manager (<c>advapi32.dll</c>) for credentials under 2 KB,
/// and DPAPI (<see cref="ProtectedData"/>) encrypted files for large payloads (e.g., YouTube Music cookies).
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
  public WindowsCredentialManagerCredentialStore()
      : this(OperatingSystem.IsWindows() ? new NativeWindowsCredentialAdapter() : new UnsupportedWindowsCredentialAdapter()) {
  }

  /// <summary>
  /// Initializes a new instance of <see cref="WindowsCredentialManagerCredentialStore"/> with a custom adapter for testing.
  /// </summary>
  /// <param name="adapter">The Windows credential adapter.</param>
  public WindowsCredentialManagerCredentialStore(IWindowsCredentialAdapter adapter) {
    _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
  }

  /// <inheritdoc />
  public Task<string?> GetCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);

    if (!_adapter.IsSupportedPlatform) {
      return Task.FromResult<string?>(null);
    }

    // 1. Check if a DPAPI encrypted file exists for large payloads
    string dpapiFilePath = GetDpapiFilePath(service, account);
    if (File.Exists(dpapiFilePath)) {
      try {
        byte[] encryptedBytes = File.ReadAllBytes(dpapiFilePath);
        byte[] decryptedBytes = _adapter.UnprotectData(encryptedBytes);
        return Task.FromResult<string?>(Encoding.UTF8.GetString(decryptedBytes));
      } catch {
        // Fall through to Credential Manager if DPAPI read fails
      }
    }

    // 2. Query Windows Credential Manager
    string target = FormatTarget(service, account);
    if (_adapter.ReadCredential(target, out string? secret)) {
      return Task.FromResult(secret);
    }

    return Task.FromResult<string?>(null);
  }

  /// <inheritdoc />
  public Task SetCredentialAsync(string service, string account, string secret, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);
    ArgumentNullException.ThrowIfNull(secret);

    if (!_adapter.IsSupportedPlatform) {
      throw new PlatformNotSupportedException("Windows Credential Manager is only supported on Windows.");
    }

    byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
    string dpapiFilePath = GetDpapiFilePath(service, account);
    string target = FormatTarget(service, account);

    if (secretBytes.Length > CredentialManagerBlobThresholdBytes) {
      // Store using DPAPI
      byte[] encrypted = _adapter.ProtectData(secretBytes);
      string dir = Path.GetDirectoryName(dpapiFilePath)!;
      if (!Directory.Exists(dir)) {
        Directory.CreateDirectory(dir);
      }
      File.WriteAllBytes(dpapiFilePath, encrypted);

      // Clean up any stale small credential in Credential Manager
      _adapter.DeleteCredential(target);
    } else {
      // Store in Windows Credential Manager
      bool success = _adapter.WriteCredential(target, account, secret);
      if (!success) {
        // If Credential Manager failed (e.g. system limit), fallback to DPAPI
        byte[] encrypted = _adapter.ProtectData(secretBytes);
        string dir = Path.GetDirectoryName(dpapiFilePath)!;
        if (!Directory.Exists(dir)) {
          Directory.CreateDirectory(dir);
        }
        File.WriteAllBytes(dpapiFilePath, encrypted);
      } else {
        // Clean up any stale DPAPI file
        if (File.Exists(dpapiFilePath)) {
          try {
            File.Delete(dpapiFilePath);
          } catch {
            // Ignore file deletion errors
          }
        }
      }
    }

    return Task.CompletedTask;
  }

  /// <inheritdoc />
  public Task DeleteCredentialAsync(string service, string account, CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(account);

    if (!_adapter.IsSupportedPlatform) {
      return Task.CompletedTask;
    }

    string target = FormatTarget(service, account);
    _adapter.DeleteCredential(target);

    string dpapiFilePath = GetDpapiFilePath(service, account);
    if (File.Exists(dpapiFilePath)) {
      try {
        File.Delete(dpapiFilePath);
      } catch {
        // Ignore file deletion errors
      }
    }

    return Task.CompletedTask;
  }

  /// <inheritdoc />
  public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) {
    cancellationToken.ThrowIfCancellationRequested();
    return Task.FromResult(_adapter.IsSupportedPlatform);
  }

  private static string FormatTarget(string service, string account) => $"{service}:{account}";

  private string GetDpapiFilePath(string service, string account) {
    string fileName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{service}:{account}"))) + ".dat";
    return Path.Combine(_adapter.CredentialsDirectory, fileName);
  }
}
