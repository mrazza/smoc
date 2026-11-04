using System.Security.Cryptography;

namespace Smoc.Security.Windows;

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
