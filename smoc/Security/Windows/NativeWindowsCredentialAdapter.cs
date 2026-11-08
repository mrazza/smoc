using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Smoc.Security.Windows;

/// <summary>
/// Native implementation of <see cref="IWindowsCredentialAdapter"/> using Windows Credential Manager (<c>advapi32.dll</c>)
/// and DPAPI (<see cref="ProtectedData"/>) encrypted files in <c>%APPDATA%\smoc\credentials</c>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class NativeWindowsCredentialAdapter : IWindowsCredentialAdapter {
  private const int CredTypeGeneric = 1;
  private const int CredPersistLocalMachine = 2;
  private const int ErrorNotFound = 1168;

  /// <summary>
  /// Directory where DPAPI encrypted blobs are persisted on Windows.
  /// Standard Windows user data location is <c>%APPDATA%\smoc\credentials</c>.
  /// </summary>
  private static readonly string CredentialsDirectory = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "smoc",
      "credentials");

  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  private struct Credential {
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
  private static extern bool CredWrite([In] ref Credential credential, int flags);

  [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
  private static extern bool CredDelete(string target, int type, int flags);

  [DllImport("advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
  private static extern void CredFree(IntPtr buffer);

  /// <inheritdoc />
  public bool ReadCredential(string target, out string? secret) {
    secret = null;

    if (CredRead(target, CredTypeGeneric, 0, out IntPtr credPtr)) {
      try {
        Credential cred = Marshal.PtrToStructure<Credential>(credPtr);
        if (cred.CredentialBlob != IntPtr.Zero && cred.CredentialBlobSize > 0) {
          byte[] bytes = new byte[cred.CredentialBlobSize];
          Marshal.Copy(cred.CredentialBlob, bytes, 0, cred.CredentialBlobSize);
          secret = Encoding.UTF8.GetString(bytes);
          return true;
        }
      } finally {
        CredFree(credPtr);
      }

      return false;
    }

    int error = Marshal.GetLastWin32Error();
    if (error != 0 && error != ErrorNotFound) {
      throw new Win32Exception(error, $"CredRead failed for target '{target}' (Win32 error {error}).");
    }

    return false;
  }

  /// <inheritdoc />
  public bool WriteCredential(string target, string userName, string secret) {
    byte[] blob = Encoding.UTF8.GetBytes(secret);
    IntPtr blobPtr = Marshal.AllocHGlobal(blob.Length);
    try {
      Marshal.Copy(blob, 0, blobPtr, blob.Length);
      Credential cred = new() {
        Type = CredTypeGeneric,
        TargetName = target,
        UserName = userName,
        CredentialBlobSize = blob.Length,
        CredentialBlob = blobPtr,
        Persist = CredPersistLocalMachine
      };

      return CredWrite(ref cred, 0);
    } finally {
      Marshal.FreeHGlobal(blobPtr);
    }
  }

  /// <inheritdoc />
  public bool DeleteCredential(string target) {
    if (CredDelete(target, CredTypeGeneric, 0)) {
      return true;
    }

    int error = Marshal.GetLastWin32Error();
    if (error == ErrorNotFound || error == 0) {
      return false;
    }

    throw new Win32Exception(error, $"CredDelete failed for target '{target}' (Win32 error {error}).");
  }

  /// <inheritdoc />
  public async Task<string?> ReadDpapiSecretAsync(string target, CancellationToken cancellationToken) {
    string filePath = GetDpapiFilePath(target);
    if (!File.Exists(filePath)) {
      return null;
    }

    byte[] encryptedBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
    byte[] decryptedBytes = ProtectedData.Unprotect(encryptedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
    return Encoding.UTF8.GetString(decryptedBytes);
  }

  /// <inheritdoc />
  public async Task WriteDpapiSecretAsync(string target, string secret, CancellationToken cancellationToken) {
    string filePath = GetDpapiFilePath(target);
    string? directory = Path.GetDirectoryName(filePath);
    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
      Directory.CreateDirectory(directory);
    }

    byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
    byte[] encryptedBytes = ProtectedData.Protect(secretBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

    string tempPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
    try {
      await File.WriteAllBytesAsync(tempPath, encryptedBytes, cancellationToken);
      File.Move(tempPath, filePath, overwrite: true);
    } finally {
      if (File.Exists(tempPath)) {
        try {
          File.Delete(tempPath);
        } catch {
          // Best effort cleanup
        }
      }
    }
  }

  /// <inheritdoc />
  public void DeleteDpapiSecret(string target) {
    string filePath = GetDpapiFilePath(target);
    if (File.Exists(filePath)) {
      File.Delete(filePath);
    }
  }

  private static string GetDpapiFilePath(string target) {
    string fileName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(target))) + ".dat";
    return Path.Combine(CredentialsDirectory, fileName);
  }
}
