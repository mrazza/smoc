using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Smoc.Security.Windows;

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

  /// <inheritdoc />
  public bool IsSupportedPlatform => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

  /// <inheritdoc />
  public string CredentialsDirectory => Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "smoc",
      "credentials");

  /// <inheritdoc />
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

  /// <inheritdoc />
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

  /// <inheritdoc />
  public bool DeleteCredential(string target) {
    if (!IsSupportedPlatform) {
      return false;
    }

    return CredDelete(target, CRED_TYPE_GENERIC, 0);
  }

  /// <inheritdoc />
  public byte[] ProtectData(byte[] userData) {
    return ProtectedData.Protect(userData, optionalEntropy: null, DataProtectionScope.CurrentUser);
  }

  /// <inheritdoc />
  public byte[] UnprotectData(byte[] encryptedData) {
    return ProtectedData.Unprotect(encryptedData, optionalEntropy: null, DataProtectionScope.CurrentUser);
  }
}
