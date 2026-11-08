namespace Smoc.Security.Windows;

/// <summary>
/// Internal abstraction over Windows Credential Manager and DPAPI encrypted payload storage for testing.
/// </summary>
internal interface IWindowsCredentialAdapter {
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
  /// Reads and decrypts a DPAPI encrypted secret payload.
  /// </summary>
  Task<string?> ReadDpapiSecretAsync(string target, CancellationToken cancellationToken);

  /// <summary>
  /// Encrypts and writes a DPAPI encrypted secret payload.
  /// </summary>
  Task WriteDpapiSecretAsync(string target, string secret, CancellationToken cancellationToken);

  /// <summary>
  /// Deletes a DPAPI encrypted secret payload.
  /// </summary>
  void DeleteDpapiSecret(string target);
}
