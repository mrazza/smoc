using System.Text.Json;
using System.Text.Json.Nodes;
using Smoc.Configuration;
using Smoc.Security;
using Smoc.Streaming.Subsonic;

namespace Smoc.Cli.Auth.Subsonic;

/// <summary>
/// Handles Subsonic authentication operations, connection verification, and credential migration.
/// </summary>
public class SubsonicAuthHandler {
  private readonly ICredentialStore _credentialStore;
  private readonly ConfigFileWriter _configWriter;
  private readonly HttpClient _httpClient;

  /// <summary>
  /// Initializes a new instance of <see cref="SubsonicAuthHandler"/>.
  /// </summary>
  /// <param name="credentialStore">The credential store implementation.</param>
  /// <param name="configWriter">The configuration file writer.</param>
  /// <param name="httpClient">The HTTP client used for server verification.</param>
  public SubsonicAuthHandler(
    ICredentialStore credentialStore,
    ConfigFileWriter configWriter,
    HttpClient httpClient
  ) {
    _credentialStore = credentialStore;
    _configWriter = configWriter;
    _httpClient = httpClient;
  }

  /// <summary>
  /// Logs into Subsonic by storing user credentials in the credential store and saving server parameters to config.
  /// </summary>
  /// <param name="host">The Subsonic server hostname or IP.</param>
  /// <param name="port">The Subsonic server port number.</param>
  /// <param name="scheme">The URL scheme (e.g. http or https).</param>
  /// <param name="username">The Subsonic username.</param>
  /// <param name="password">The Subsonic password.</param>
  /// <param name="useToken">Whether to use token-based authentication.</param>
  /// <param name="verifyConnection">Whether to verify connection to the Subsonic server before saving.</param>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  public async Task LoginAsync(
    string host,
    int port,
    string scheme,
    string username,
    string password,
    bool useToken,
    bool verifyConnection = true,
    CancellationToken cancellationToken = default
  ) {
    ArgumentException.ThrowIfNullOrWhiteSpace(host);
    ArgumentException.ThrowIfNullOrWhiteSpace(username);
    ArgumentException.ThrowIfNullOrWhiteSpace(password);

    if (verifyConnection) {
      await VerifyConnectionAsync(host, port, scheme, username, password, useToken, cancellationToken);
    }

    await _credentialStore.SetCredentialAsync(CredentialTargets.Subsonic, username, password, cancellationToken);

    await _configWriter.UpdateAsync(rootNode => {
      JsonObject subsonicNode = AuthCredentialHelper.GetOrAddObject(rootNode, nameof(SubsonicConfig));
      subsonicNode[nameof(SubsonicConfig.ServerHost)] = host;
      subsonicNode[nameof(SubsonicConfig.ServerPort)] = port;
      subsonicNode[nameof(SubsonicConfig.ServerScheme)] = scheme;
      subsonicNode[nameof(SubsonicConfig.Username)] = username;
      subsonicNode[nameof(SubsonicConfig.UseToken)] = useToken;
      subsonicNode.Remove(nameof(SubsonicConfig.Password));
    }, backupOriginal: false, cancellationToken);
  }

  /// <summary>
  /// Verifies connection and authentication against a Subsonic server using the ping endpoint.
  /// </summary>
  /// <param name="host">The Subsonic server hostname or IP.</param>
  /// <param name="port">The Subsonic server port number.</param>
  /// <param name="scheme">The URL scheme (e.g. http or https).</param>
  /// <param name="username">The Subsonic username.</param>
  /// <param name="password">The Subsonic password.</param>
  /// <param name="useToken">Whether to use token-based authentication.</param>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  public async Task VerifyConnectionAsync(
    string host,
    int port,
    string scheme,
    string username,
    string password,
    bool useToken,
    CancellationToken cancellationToken = default
  ) {
    Uri uri = SubsonicApi.BuildUri(scheme, host, port, "ping.view", username, password, useToken);

    using HttpResponseMessage response = await _httpClient.GetAsync(uri, cancellationToken);
    response.EnsureSuccessStatusCode();

    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
    using JsonDocument doc = JsonDocument.Parse(responseBody);
    SubsonicApi.ValidateResponse(doc.RootElement);
  }

  /// <summary>
  /// Retrieves the current authentication and configuration status for Subsonic.
  /// </summary>
  /// <param name="rootNode">The parsed configuration root node, if available.</param>
  /// <param name="configError">Any error encountered while loading configuration.</param>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  /// <returns>A <see cref="ServiceAuthStatus"/> for Subsonic.</returns>
  public async Task<ServiceAuthStatus> GetStatusAsync(
    JsonObject? rootNode,
    string? configError,
    CancellationToken cancellationToken = default
  ) {
    if (configError != null) {
      return new ServiceAuthStatus("Subsonic", AuthStatusState.Error, CredentialStorageKind.None, $"Config error: {configError}");
    }

    JsonObject? subsonicNode = rootNode?[nameof(SubsonicConfig)] as JsonObject;
    string? host = subsonicNode?[nameof(SubsonicConfig.ServerHost)]?.GetValue<string>();
    string? username = subsonicNode?[nameof(SubsonicConfig.Username)]?.GetValue<string>();
    string? plaintextSubsonicPassword = subsonicNode?[nameof(SubsonicConfig.Password)]?.GetValue<string>();

    if (!string.IsNullOrEmpty(username)) {
      CredentialLookup lookup = await AuthCredentialHelper.InspectCredentialAsync(_credentialStore, CredentialTargets.Subsonic, username, cancellationToken);
      if (lookup.Error != null) {
        return new ServiceAuthStatus("Subsonic", AuthStatusState.Error, CredentialStorageKind.None, $"Keyring error: {lookup.Error}");
      }
      if (!string.IsNullOrEmpty(lookup.Value)) {
        return new ServiceAuthStatus("Subsonic", AuthStatusState.Authenticated, lookup.Storage, $"{username} @ {host ?? "unspecified"}");
      }
      if (!string.IsNullOrEmpty(plaintextSubsonicPassword)) {
        return new ServiceAuthStatus("Subsonic", AuthStatusState.ConfiguredInsecure, CredentialStorageKind.PlaintextConfig, $"{username} @ {host ?? "unspecified"} (Run 'smoc auth migrate')");
      }
      return new ServiceAuthStatus("Subsonic", AuthStatusState.MissingCredentials, CredentialStorageKind.None, $"{username} @ {host ?? "unspecified"} (Password missing)");
    }

    return new ServiceAuthStatus("Subsonic", AuthStatusState.NotConfigured, CredentialStorageKind.None, "No username/host configured in config.json");
  }

  /// <summary>
  /// Migrates plaintext Subsonic credentials from configuration to the credential store.
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
    if (rootNode[nameof(SubsonicConfig)] is not JsonObject subsonicNode) {
      return false;
    }

    string? username = subsonicNode[nameof(SubsonicConfig.Username)]?.GetValue<string>();
    string? password = subsonicNode[nameof(SubsonicConfig.Password)]?.GetValue<string>();

    if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) {
      return false;
    }

    try {
      await _credentialStore.SetCredentialAsync(CredentialTargets.Subsonic, username, password, cancellationToken);
      subsonicNode.Remove(nameof(SubsonicConfig.Password));
      migratedItems.Add($"Subsonic password for user '{username}'");
      return true;
    } catch (OperationCanceledException) {
      throw;
    } catch (Exception ex) {
      errors.Add($"Failed to migrate Subsonic password: {ex.Message}");
      return false;
    }
  }

  /// <summary>
  /// Logs out of Subsonic by deleting stored credentials.
  /// </summary>
  /// <param name="rootNode">The parsed configuration root node, if available.</param>
  /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
  /// <returns><c>true</c> if credentials were removed; otherwise, <c>false</c>.</returns>
  public async Task<bool> LogoutAsync(JsonObject? rootNode, CancellationToken cancellationToken = default) {
    string? username = rootNode?[nameof(SubsonicConfig)]?[nameof(SubsonicConfig.Username)]?.GetValue<string>();
    if (!string.IsNullOrEmpty(username)) {
      return await AuthCredentialHelper.DeleteCredentialIfPresentAsync(_credentialStore, CredentialTargets.Subsonic, username, cancellationToken);
    }

    return false;
  }
}

