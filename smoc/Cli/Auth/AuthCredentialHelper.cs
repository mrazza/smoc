using System.Text.Json.Nodes;
using Smoc.Security;

namespace Smoc.Cli.Auth;

/// <summary>
/// Provides shared utility methods for credential inspection, deletion, and configuration manipulation.
/// </summary>
internal static class AuthCredentialHelper {
  /// <summary>
  /// Inspects a credential from the given credential store, determining its storage kind and checking for errors.
  /// </summary>
  /// <param name="credentialStore">The credential store to inspect.</param>
  /// <param name="service">The service target name.</param>
  /// <param name="account">The account or credential key.</param>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  /// <returns>A <see cref="CredentialLookup"/> describing the found credential and storage kind.</returns>
  public static async Task<CredentialLookup> InspectCredentialAsync(
    ICredentialStore credentialStore,
    string service,
    string account,
    CancellationToken cancellationToken
  ) {
    try {
      if (credentialStore is CompositeCredentialStore composite) {
        var (value, store) = await composite.GetCredentialWithSourceAsync(service, account, cancellationToken);
        if (value != null) {
          CredentialStorageKind storage = store is EnvironmentCredentialStore
              ? CredentialStorageKind.Environment
              : CredentialStorageKind.Keyring;
          return new CredentialLookup(value, storage, null);
        }

        return new CredentialLookup(null, CredentialStorageKind.None, null);
      }

      if (await credentialStore.IsAvailableAsync(cancellationToken)) {
        string? value = await credentialStore.GetCredentialAsync(service, account, cancellationToken);
        if (value != null) {
          CredentialStorageKind storage = credentialStore is EnvironmentCredentialStore
              ? CredentialStorageKind.Environment
              : CredentialStorageKind.Keyring;
          return new CredentialLookup(value, storage, null);
        }
      }

      return new CredentialLookup(null, CredentialStorageKind.None, null);
    } catch (OperationCanceledException) {
      throw;
    } catch (Exception ex) {
      return new CredentialLookup(null, CredentialStorageKind.None, ex.InnerException?.Message ?? ex.Message);
    }
  }

  /// <summary>
  /// Deletes a credential from the credential store if currently present.
  /// </summary>
  /// <param name="credentialStore">The credential store to delete from.</param>
  /// <param name="service">The service target name.</param>
  /// <param name="account">The account or credential key.</param>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  /// <returns><c>true</c> if the credential was removed; otherwise, <c>false</c>.</returns>
  public static async Task<bool> DeleteCredentialIfPresentAsync(
    ICredentialStore credentialStore,
    string service,
    string account,
    CancellationToken cancellationToken
  ) {
    CredentialLookup before = await InspectCredentialAsync(credentialStore, service, account, cancellationToken);
    if (before.Error != null) {
      throw new InvalidOperationException(before.Error);
    }

    if (before.Value == null) {
      return false;
    }

    try {
      await credentialStore.DeleteCredentialAsync(service, account, cancellationToken);
    } catch (NotSupportedException) {
      return false;
    }

    if (before.Storage == CredentialStorageKind.Keyring) {
      return true;
    }

    CredentialLookup after = await InspectCredentialAsync(credentialStore, service, account, cancellationToken);
    return after.Value == null;
  }

  /// <summary>
  /// Retrieves or creates a child <see cref="JsonObject"/> with the specified property name.
  /// </summary>
  /// <param name="parent">The parent JSON object.</param>
  /// <param name="propertyName">The property name of the child object.</param>
  /// <returns>The existing or newly created child <see cref="JsonObject"/>.</returns>
  public static JsonObject GetOrAddObject(JsonObject parent, string propertyName) {
    if (parent[propertyName] is JsonObject existing) {
      return existing;
    }
    JsonObject newObj = new();
    parent[propertyName] = newObj;
    return newObj;
  }

  /// <summary>
  /// Sanitizes or backs up a plaintext file on disk.
  /// </summary>
  /// <param name="filePath">Path to the plaintext file.</param>
  /// <param name="backup">Whether to create a .bak backup before deleting.</param>
  /// <param name="cleanedFiles">List tracking cleaned file paths.</param>
  public static void CleanPlaintextFile(string filePath, bool backup, List<string> cleanedFiles) {
    if (!File.Exists(filePath)) {
      return;
    }

    if (backup) {
      string backupPath = filePath + ".bak";
      File.Move(filePath, backupPath, overwrite: true);
      cleanedFiles.Add($"{filePath} (renamed to .bak)");
    } else {
      File.Delete(filePath);
      cleanedFiles.Add(filePath);
    }
  }
}

