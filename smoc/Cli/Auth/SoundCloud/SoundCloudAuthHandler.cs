using System.Text.Json.Nodes;
using Smoc.Configuration;
using Smoc.Security;

namespace Smoc.Cli.Auth.SoundCloud;

/// <summary>
/// Handles SoundCloud authentication operations, status reporting, and credential migration.
/// </summary>
public class SoundCloudAuthHandler {
  private readonly ICredentialStore _credentialStore;
  private readonly ConfigFileWriter _configWriter;

  /// <summary>
  /// Initializes a new instance of <see cref="SoundCloudAuthHandler"/>.
  /// </summary>
  /// <param name="credentialStore">The credential store implementation.</param>
  /// <param name="configWriter">The configuration file writer.</param>
  public SoundCloudAuthHandler(
    ICredentialStore credentialStore,
    ConfigFileWriter configWriter
  ) {
    _credentialStore = credentialStore;
    _configWriter = configWriter;
  }

  /// <summary>
  /// Logs into SoundCloud by storing the authentication token in the credential store and optional client ID in config.
  /// </summary>
  /// <param name="clientId">The SoundCloud client ID.</param>
  /// <param name="authToken">The SoundCloud OAuth authentication token.</param>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  public async Task LoginAsync(
    string clientId,
    string authToken,
    CancellationToken cancellationToken = default
  ) {
    ArgumentException.ThrowIfNullOrWhiteSpace(authToken);

    await _credentialStore.SetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, authToken, cancellationToken);

    await _configWriter.UpdateAsync(rootNode => {
      JsonObject soundCloudNode = AuthCredentialHelper.GetOrAddObject(rootNode, nameof(SoundCloudConfig));
      if (!string.IsNullOrWhiteSpace(clientId)) {
        soundCloudNode[nameof(SoundCloudConfig.ClientId)] = clientId;
      }
      soundCloudNode.Remove(nameof(SoundCloudConfig.AuthToken));
    }, backupOriginal: false, cancellationToken);
  }

  /// <summary>
  /// Retrieves the current authentication and configuration status for SoundCloud.
  /// </summary>
  /// <param name="rootNode">The parsed configuration root node, if available.</param>
  /// <param name="configError">Any error encountered while loading configuration.</param>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  /// <returns>A <see cref="ServiceAuthStatus"/> for SoundCloud.</returns>
  public async Task<ServiceAuthStatus> GetStatusAsync(
    JsonObject? rootNode,
    string? configError,
    CancellationToken cancellationToken = default
  ) {
    if (configError != null) {
      return new ServiceAuthStatus("SoundCloud", AuthStatusState.Error, CredentialStorageKind.None, $"Config error: {configError}");
    }

    JsonObject? soundCloudNode = rootNode?[nameof(SoundCloudConfig)] as JsonObject;
    string? plaintextAuthToken = soundCloudNode?[nameof(SoundCloudConfig.AuthToken)]?.GetValue<string>();

    CredentialLookup lookup = await AuthCredentialHelper.InspectCredentialAsync(_credentialStore, CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, cancellationToken);
    if (lookup.Error != null) {
      return new ServiceAuthStatus("SoundCloud", AuthStatusState.Error, CredentialStorageKind.None, $"Keyring error: {lookup.Error}");
    }
    if (!string.IsNullOrEmpty(lookup.Value)) {
      return new ServiceAuthStatus("SoundCloud", AuthStatusState.Authenticated, lookup.Storage, "OAuth token stored");
    }
    if (!string.IsNullOrEmpty(plaintextAuthToken)) {
      return new ServiceAuthStatus("SoundCloud", AuthStatusState.ConfiguredInsecure, CredentialStorageKind.PlaintextConfig, "Plaintext AuthToken found (Run 'smoc auth migrate')");
    }

    return new ServiceAuthStatus("SoundCloud", AuthStatusState.NotConfigured, CredentialStorageKind.None, "No credentials stored");
  }

  /// <summary>
  /// Migrates plaintext SoundCloud credentials from configuration to the credential store.
  /// </summary>
  /// <param name="rootNode">The configuration root JSON object.</param>
  /// <param name="migratedItems">List tracking successfully migrated items.</param>
  /// <param name="errors">List tracking migration errors.</param>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  /// <returns><c>true</c> if configuration was modified and needs saving; otherwise, <c>false</c>.</returns>
  public async Task<bool> MigrateAsync(
    JsonObject rootNode,
    List<string> migratedItems,
    List<string> errors,
    CancellationToken cancellationToken = default
  ) {
    if (rootNode[nameof(SoundCloudConfig)] is not JsonObject soundCloudNode) {
      return false;
    }

    string? authToken = soundCloudNode[nameof(SoundCloudConfig.AuthToken)]?.GetValue<string>();

    if (string.IsNullOrEmpty(authToken)) {
      return false;
    }

    try {
      await _credentialStore.SetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, authToken, cancellationToken);
      soundCloudNode.Remove(nameof(SoundCloudConfig.AuthToken));
      migratedItems.Add("SoundCloud authentication token");
      return true;
    } catch (OperationCanceledException) {
      throw;
    } catch (Exception ex) {
      errors.Add($"Failed to migrate SoundCloud token: {ex.Message}");
      return false;
    }
  }

  /// <summary>
  /// Logs out of SoundCloud by deleting stored credentials.
  /// </summary>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  /// <returns><c>true</c> if credentials were removed; otherwise, <c>false</c>.</returns>
  public async Task<bool> LogoutAsync(CancellationToken cancellationToken = default) {
    bool removedToken = await AuthCredentialHelper.DeleteCredentialIfPresentAsync(_credentialStore, CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, cancellationToken);
    bool removedClient = await AuthCredentialHelper.DeleteCredentialIfPresentAsync(_credentialStore, CredentialTargets.SoundCloud, CredentialTargets.AccountClientId, cancellationToken);
    return removedToken || removedClient;
  }
}

