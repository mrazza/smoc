using System.Security.Cryptography;

namespace Smoc.Security.Windows;

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
