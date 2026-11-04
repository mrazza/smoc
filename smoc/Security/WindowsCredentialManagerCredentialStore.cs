using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Smoc.Security;

/// <summary>
/// Abstraction over Windows Credential Manager and DPAPI operations for unit testability across operating systems.
/// </summary>
public interface IWindowsCredentialAdapter {
  /// <summary>
  /// Gets whether Windows Credential Manager and DPAPI are supported on the current operating system.
  /// </summary>
  bool IsSupportedPlatform { get; }

  /// <summary>
  /// Gets the directory where DPAPI encrypted blobs are stored.
  /// </summary>
  string CredentialsDirectory { get; }

  /// <summary>
  /// Reads a credential from Windows Credential Manager.
  /// </summary>
  bool ReadCredential(string target, out string? secret);

  /// <summary>
  /// Writes a credential to Windows Credential Manager.
  /// </summary>
  bool WriteCredential(string target, string userName, string secret);

  /// <summary>
  /// Deletes a credential from Windows Credential Manager.
  /// </summary>
  bool DeleteCredential(string target);

  /// <summary>
  /// Encrypts data using DPAPI (<see cref="DataProtectionScope.CurrentUser"/>).
  /// </summary>
  byte[] ProtectData(byte[] userData);

  /// <summary>
  /// Decrypts data using DPAPI (<see cref="DataProtectionScope.CurrentUser"/>).
  /// </summary>
  byte[] UnprotectData(byte[] encryptedData);
}

/// <summary>
/// Native implementation of <see cref="IWindowsCredentialAdapter"/> using <c>advapi32.dll</c> and <see cref="ProtectedData"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class NativeWindowsCredentialAdapter : IWindowsCredentialAdapter {
  private const int CRED_TYPE_GENERIC = 1;
  private const int CRED_PERSIST_LOCAL_MACHINE = 2;

  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  private struct CREDENTIAL {
    public int Flags;
    public int Type;
    public string TargetName;
    public string Comment;
    public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
    public int CredentialBlobSize;
    public IntPtr CredentialBlob;
    public int Persist;
    public int AttributeCount;
    public IntPtr Attributes;
    public string TargetAlias;
    public string UserName;
  }

  [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
  private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

  [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
  private static extern bool CredWrite([In] ref CREDENTIAL credential, int flags);

  [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
  private static extern bool CredDelete(string target, int type, int flags);

  [DllImport("advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
  private static extern void CredFree(IntPtr buffer);

  public bool IsSupportedPlatform => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

  public string CredentialsDirectory => Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "smoc",
      "credentials");

  public bool ReadCredential(string target, out string? secret) {
    secret = null;
    if (!IsSupportedPlatform) {
      return false;
    }

    if (CredRead(target, CRED_TYPE_GENERIC, 0, out IntPtr credPtr)) {
      try {
        CREDENTIAL cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
        if (cred.CredentialBlob != IntPtr.Zero && cred.CredentialBlobSize > 0) {
          byte[] bytes = new byte[cred.CredentialBlobSize];
          Marshal.Copy(cred.CredentialBlob, bytes, 0, cred.CredentialBlobSize);
          secret = Encoding.UTF8.GetString(bytes);
          return true;
        }
      } finally {
        CredFree(credPtr);
      }
    }

    return false;
  }

  public bool WriteCredential(string target, string userName, string secret) {
    if (!IsSupportedPlatform) {
      return false;
    }

    byte[] blob = Encoding.UTF8.GetBytes(secret);
    IntPtr blobPtr = Marshal.AllocHGlobal(blob.Length);
    try {
      Marshal.Copy(blob, 0, blobPtr, blob.Length);
      CREDENTIAL cred = new() {
        Type = CRED_TYPE_GENERIC,
        TargetName = target,
        UserName = userName,
        CredentialBlobSize = blob.Length,
        CredentialBlob = blobPtr,
        Persist = CRED_PERSIST_LOCAL_MACHINE
      };

      return CredWrite(ref cred, 0);
    } finally {
      Marshal.FreeHGlobal(blobPtr);
    }
  }

  public bool DeleteCredential(string target) {
    if (!IsSupportedPlatform) {
      return false;
    }

    return CredDelete(target, CRED_TYPE_GENERIC, 0);
  }

  public byte[] ProtectData(byte[] userData) {
    return ProtectedData.Protect(userData, optionalEntropy: null, DataProtectionScope.CurrentUser);
  }

  public byte[] UnprotectData(byte[] encryptedData) {
    return ProtectedData.Unprotect(encryptedData, optionalEntropy: null, DataProtectionScope.CurrentUser);
  }
}

/// <summary>
/// Fallback adapter used when executing on non-Windows platforms.
/// </summary>
internal sealed class UnsupportedWindowsCredentialAdapter : IWindowsCredentialAdapter {
  public bool IsSupportedPlatform => false;
  public string CredentialsDirectory => string.Empty;
  public bool ReadCredential(string target, out string? secret) {
    secret = null;
    return false;
  }
  public bool WriteCredential(string target, string userName, string secret) => false;
  public bool DeleteCredential(string target) => false;
  public byte[] ProtectData(byte[] userData) => throw new PlatformNotSupportedException("DPAPI is only supported on Windows.");
  public byte[] UnprotectData(byte[] encryptedData) => throw new PlatformNotSupportedException("DPAPI is only supported on Windows.");
}

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
