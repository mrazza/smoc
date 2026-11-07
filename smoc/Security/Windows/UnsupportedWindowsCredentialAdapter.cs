using System.Security.Cryptography;

namespace Smoc.Security.Windows;

/// <summary>
/// Fallback adapter used when executing on non-Windows platforms.
/// </summary>
internal sealed class UnsupportedWindowsCredentialAdapter : IWindowsCredentialAdapter {
  /// <inheritdoc />
  public bool IsSupportedPlatform => false;

  /// <inheritdoc />
  public string CredentialsDirectory => string.Empty;

  /// <inheritdoc />
  public bool ReadCredential(string target, out string? secret) {
    secret = null;
    return false;
  }

  /// <inheritdoc />
  public bool WriteCredential(string target, string userName, string secret) => false;

  /// <inheritdoc />
  public bool DeleteCredential(string target) => false;

  /// <inheritdoc />
  public byte[] ProtectData(byte[] userData) => throw new PlatformNotSupportedException("DPAPI is only supported on Windows.");

  /// <inheritdoc />
  public byte[] UnprotectData(byte[] encryptedData) => throw new PlatformNotSupportedException("DPAPI is only supported on Windows.");
}
