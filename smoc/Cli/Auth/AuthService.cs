using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Smoc.Configuration;
using Smoc.Security;
using Smoc.Streaming.Subsonic;
using Smoc.Streaming.YouTubeMusic;

namespace Smoc.Cli.Auth;

/// <summary>
/// Provides streaming service authentication management, credential migration, and status reporting.
/// </summary>
public class AuthService : IAuthService, IDisposable {
  private readonly ICredentialStore _credentialStore;
  private readonly ConfigFileWriter _configWriter;
  private readonly string _cookiesPath;
  private readonly string _tokensPath;
  private readonly Func<List<Cookie>, Task<YtmTokens>> _tokenGenerator;
  private readonly HttpClient _httpClient;
  private readonly bool _ownsHttpClient;
  private bool _disposed;

  /// <summary>
  /// Initializes a new instance of <see cref="AuthService"/> with default user profile paths.
  /// </summary>
  public AuthService() : this(
    CredentialStoreFactory.CreateDefault(),
    SmocPaths.DefaultConfigPath,
    SmocPaths.DefaultCookiesPath,
    SmocPaths.DefaultTokensPath,
    YtmStreamingClient.GenerateTokensAsync,
    httpClient: null
  ) { }

  /// <summary>
  /// Initializes a new instance of <see cref="AuthService"/> with explicit dependencies.
  /// </summary>
  /// <param name="credentialStore">The credential store implementation.</param>
  /// <param name="configPath">Path to config.json.</param>
  /// <param name="cookiesPath">Path to cookies.txt.</param>
  /// <param name="tokensPath">Path to tokens.json.</param>
  /// <param name="tokenGenerator">Function to generate YouTube Music tokens from cookies.</param>
  /// <param name="httpClient">HTTP client used for server verification.</param>
  public AuthService(
    ICredentialStore credentialStore,
    string configPath,
    string cookiesPath,
    string tokensPath,
    Func<List<Cookie>, Task<YtmTokens>>? tokenGenerator = null,
    HttpClient? httpClient = null
  ) {
    _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
    _configWriter = new ConfigFileWriter(configPath ?? throw new ArgumentNullException(nameof(configPath)));
    _cookiesPath = cookiesPath ?? throw new ArgumentNullException(nameof(cookiesPath));
    _tokensPath = tokensPath ?? throw new ArgumentNullException(nameof(tokensPath));
    _tokenGenerator = tokenGenerator ?? YtmStreamingClient.GenerateTokensAsync;
    _ownsHttpClient = httpClient == null;
    _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
  }

  /// <inheritdoc />
  public async Task LoginSubsonicAsync(
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
      await VerifySubsonicConnectionAsync(host, port, scheme, username, password, useToken, cancellationToken);
    }

    await _credentialStore.SetCredentialAsync(CredentialTargets.Subsonic, username, password, cancellationToken);

    await _configWriter.UpdateAsync(rootNode => {
      JsonObject subsonicNode = GetOrAddObject(rootNode, nameof(SubsonicConfig));
      subsonicNode[nameof(SubsonicConfig.ServerHost)] = host;
      subsonicNode[nameof(SubsonicConfig.ServerPort)] = port;
      subsonicNode[nameof(SubsonicConfig.ServerScheme)] = scheme;
      subsonicNode[nameof(SubsonicConfig.Username)] = username;
      subsonicNode[nameof(SubsonicConfig.UseToken)] = useToken;
      subsonicNode.Remove(nameof(SubsonicConfig.Password));
    }, backupOriginal: false, cancellationToken);
  }

  /// <inheritdoc />
  public async Task LoginSoundCloudAsync(
    string clientId,
    string authToken,
    CancellationToken cancellationToken = default
  ) {
    ArgumentException.ThrowIfNullOrWhiteSpace(authToken);

    await _credentialStore.SetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, authToken, cancellationToken);

    await _configWriter.UpdateAsync(rootNode => {
      JsonObject soundCloudNode = GetOrAddObject(rootNode, nameof(SoundCloudConfig));
      if (!string.IsNullOrWhiteSpace(clientId)) {
        soundCloudNode[nameof(SoundCloudConfig.ClientId)] = clientId;
      }
      soundCloudNode.Remove(nameof(SoundCloudConfig.AuthToken));
    }, backupOriginal: false, cancellationToken);
  }

  /// <inheritdoc />
  public async Task<ImportCookiesResult> ImportCookiesAsync(
    string? cookieFilePath = null,
    bool deleteSource = false,
    CancellationToken cancellationToken = default
  ) {
    string resolvedPath = string.IsNullOrWhiteSpace(cookieFilePath) ? _cookiesPath : cookieFilePath;
    if (!File.Exists(resolvedPath)) {
      return ImportCookiesResult.Failed($"Cookies file not found at '{resolvedPath}'.");
    }

    try {
      string content = await File.ReadAllTextAsync(resolvedPath, cancellationToken);
      if (string.IsNullOrWhiteSpace(content)) {
        return ImportCookiesResult.Failed($"Cookies file at '{resolvedPath}' is empty.");
      }

      List<Cookie> cookies = YtmStreamingClient.ParseCookies(content);
      if (cookies.Count == 0) {
        return ImportCookiesResult.Failed($"No valid cookies could be parsed from '{resolvedPath}'.");
      }

      await _credentialStore.SetCredentialAsync(
          CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, content.Trim(), cancellationToken);

      YtmTokens tokens = await _tokenGenerator(cookies);
      string serializedTokens = JsonSerializer.Serialize(tokens);
      await _credentialStore.SetCredentialAsync(
          CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, serializedTokens, cancellationToken);

      if (deleteSource) {
        try {
          File.Delete(resolvedPath);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
          return ImportCookiesResult.Succeeded(
              $"Successfully imported {cookies.Count} cookies and generated visitor/PO tokens, but failed to delete source file: {ex.Message}",
              cookies.Count,
              tokensGenerated: true,
              sourceDeleted: false);
        }
      }

      return ImportCookiesResult.Succeeded(
          $"Successfully imported {cookies.Count} cookies and generated visitor/PO tokens.",
          cookies.Count,
          tokensGenerated: true,
          sourceDeleted: deleteSource);
    } catch (OperationCanceledException) {
      throw;
    } catch (Exception ex) {
      return ImportCookiesResult.Failed($"Failed to import cookies: {ex.Message}");
    }
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<ServiceAuthStatus>> GetStatusAsync(CancellationToken cancellationToken = default) {
    JsonObject? rootNode = null;
    string? configError = null;
    try {
      rootNode = await _configWriter.LoadAsync(cancellationToken);
    } catch (OperationCanceledException) {
      throw;
    } catch (Exception ex) {
      configError = ex.Message;
    }

    return [
      await GetSubsonicStatusAsync(rootNode, configError, cancellationToken),
      await GetYouTubeMusicStatusAsync(cancellationToken),
      await GetSoundCloudStatusAsync(rootNode, configError, cancellationToken)
    ];
  }

  /// <inheritdoc />
  public async Task<bool> LogoutAsync(string serviceName, CancellationToken cancellationToken = default) {
    ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

    if (!AuthServiceTargetParser.TryParse(serviceName, out AuthServiceTarget target)) {
      throw new ArgumentException($"Unknown service '{serviceName}'. Supported services are: subsonic, soundcloud, youtube, all.", nameof(serviceName));
    }

    bool anyRemoved = false;

    if (target is AuthServiceTarget.Subsonic or AuthServiceTarget.All) {
      JsonObject? rootNode = null;
      try {
        rootNode = await _configWriter.LoadAsync(cancellationToken);
      } catch (OperationCanceledException) {
        throw;
      } catch {
        // Best effort config load
      }
      string? username = rootNode?[nameof(SubsonicConfig)]?[nameof(SubsonicConfig.Username)]?.GetValue<string>();
      if (!string.IsNullOrEmpty(username)) {
        anyRemoved |= await DeleteCredentialIfPresentAsync(CredentialTargets.Subsonic, username, cancellationToken);
      }
    }

    if (target is AuthServiceTarget.YouTubeMusic or AuthServiceTarget.All) {
      anyRemoved |= await DeleteCredentialIfPresentAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, cancellationToken);
      anyRemoved |= await DeleteCredentialIfPresentAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, cancellationToken);
    }

    if (target is AuthServiceTarget.SoundCloud or AuthServiceTarget.All) {
      anyRemoved |= await DeleteCredentialIfPresentAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, cancellationToken);
      anyRemoved |= await DeleteCredentialIfPresentAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountClientId, cancellationToken);
    }

    return anyRemoved;
  }

  /// <inheritdoc />
  public async Task<MigrationResult> MigrateAsync(bool backupPlaintextFiles = false, CancellationToken cancellationToken = default) {
    List<string> migratedItems = [];
    List<string> cleanedFiles = [];
    List<string> errors = [];

    JsonObject? rootNode = null;
    try {
      rootNode = await _configWriter.LoadAsync(cancellationToken);
    } catch (OperationCanceledException) {
      throw;
    } catch (Exception ex) {
      errors.Add($"Failed to read config.json: {ex.Message}");
    }

    if (rootNode != null) {
      bool subsonicModified = await MigrateSubsonicAsync(rootNode, migratedItems, errors, cancellationToken);
      bool soundCloudModified = await MigrateSoundCloudAsync(rootNode, migratedItems, errors, cancellationToken);

      if (subsonicModified || soundCloudModified) {
        try {
          await _configWriter.SaveAsync(rootNode, backupOriginal: false, cancellationToken);
          cleanedFiles.Add(_configWriter.ConfigPath);
        } catch (OperationCanceledException) {
          throw;
        } catch (Exception ex) {
          errors.Add($"Failed to sanitize config.json: {ex.Message}");
        }
      }
    }

    await MigrateYouTubeMusicAsync(backupPlaintextFiles, migratedItems, cleanedFiles, errors, cancellationToken);

    return new MigrationResult(errors.Count == 0, migratedItems, cleanedFiles, errors);
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed) {
      return;
    }

    _disposed = true;
    if (_ownsHttpClient) {
      _httpClient.Dispose();
    }

    if (_credentialStore is IDisposable disposableStore) {
      disposableStore.Dispose();
    }
  }

  private async Task<bool> DeleteCredentialIfPresentAsync(string service, string account, CancellationToken cancellationToken) {
    CredentialLookup before = await InspectCredentialAsync(service, account, cancellationToken);
    if (before.Error != null) {
      throw new InvalidOperationException(before.Error);
    }

    if (before.Value == null) {
      return false;
    }

    try {
      await _credentialStore.DeleteCredentialAsync(service, account, cancellationToken);
    } catch (NotSupportedException) {
      return false;
    }

    if (before.Storage == CredentialStorageKind.Keyring) {
      return true;
    }

    CredentialLookup after = await InspectCredentialAsync(service, account, cancellationToken);
    return after.Value == null;
  }

  private async Task<bool> MigrateSubsonicAsync(
    JsonObject rootNode,
    List<string> migratedItems,
    List<string> errors,
    CancellationToken cancellationToken
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

  private async Task<bool> MigrateSoundCloudAsync(
    JsonObject rootNode,
    List<string> migratedItems,
    List<string> errors,
    CancellationToken cancellationToken
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

  private async Task MigrateYouTubeMusicAsync(
    bool backupPlaintextFiles,
    List<string> migratedItems,
    List<string> cleanedFiles,
    List<string> errors,
    CancellationToken cancellationToken
  ) {
    if (!File.Exists(_cookiesPath)) {
      if (File.Exists(_tokensPath)) {
        CleanPlaintextFile(_tokensPath, backupPlaintextFiles, cleanedFiles);
      }
      return;
    }

    try {
      string cookieContent = await File.ReadAllTextAsync(_cookiesPath, cancellationToken);
      if (!string.IsNullOrWhiteSpace(cookieContent)) {
        await _credentialStore.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, cookieContent.Trim(), cancellationToken);
        migratedItems.Add("YouTube Music session cookies");

        if (File.Exists(_tokensPath)) {
          string tokenContent = await File.ReadAllTextAsync(_tokensPath, cancellationToken);
          if (!string.IsNullOrWhiteSpace(tokenContent)) {
            await _credentialStore.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, tokenContent.Trim(), cancellationToken);
            migratedItems.Add("YouTube Music visitor and PO tokens");
          }
        } else {
          List<Cookie> parsedCookies = YtmStreamingClient.ParseCookies(cookieContent);
          if (parsedCookies.Count > 0) {
            YtmTokens generated = await _tokenGenerator(parsedCookies);
            await _credentialStore.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, JsonSerializer.Serialize(generated), cancellationToken);
            migratedItems.Add("Generated YouTube Music visitor and PO tokens");
          }
        }

        CleanPlaintextFile(_cookiesPath, backupPlaintextFiles, cleanedFiles);
        CleanPlaintextFile(_tokensPath, backupPlaintextFiles, cleanedFiles);
      }
    } catch (OperationCanceledException) {
      throw;
    } catch (Exception ex) {
      errors.Add($"Failed to migrate YouTube Music cookies/tokens: {ex.Message}");
    }
  }

  private static void CleanPlaintextFile(string filePath, bool backup, List<string> cleanedFiles) {
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

  private async Task VerifySubsonicConnectionAsync(
    string host,
    int port,
    string scheme,
    string username,
    string password,
    bool useToken,
    CancellationToken cancellationToken
  ) {
    Uri uri = SubsonicApi.BuildUri(scheme, host, port, "ping.view", username, password, useToken);

    using HttpResponseMessage response = await _httpClient.GetAsync(uri, cancellationToken);
    response.EnsureSuccessStatusCode();

    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
    using JsonDocument doc = JsonDocument.Parse(responseBody);
    SubsonicApi.ValidateResponse(doc.RootElement);
  }

  private sealed record CredentialLookup(string? Value, CredentialStorageKind Storage, string? Error);

  private async Task<CredentialLookup> InspectCredentialAsync(string service, string account, CancellationToken cancellationToken) {
    try {
      if (_credentialStore is CompositeCredentialStore composite) {
        var (value, store) = await composite.GetCredentialWithSourceAsync(service, account, cancellationToken);
        if (value != null) {
          CredentialStorageKind storage = store is EnvironmentCredentialStore
              ? CredentialStorageKind.Environment
              : CredentialStorageKind.Keyring;
          return new CredentialLookup(value, storage, null);
        }

        return new CredentialLookup(null, CredentialStorageKind.None, null);
      }

      if (await _credentialStore.IsAvailableAsync(cancellationToken)) {
        string? value = await _credentialStore.GetCredentialAsync(service, account, cancellationToken);
        if (value != null) {
          CredentialStorageKind storage = _credentialStore is EnvironmentCredentialStore
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

  private async Task<ServiceAuthStatus> GetSubsonicStatusAsync(JsonObject? rootNode, string? configError, CancellationToken cancellationToken) {
    if (configError != null) {
      return new ServiceAuthStatus("Subsonic", AuthStatusState.Error, CredentialStorageKind.None, $"Config error: {configError}");
    }

    JsonObject? subsonicNode = rootNode?[nameof(SubsonicConfig)] as JsonObject;
    string? host = subsonicNode?[nameof(SubsonicConfig.ServerHost)]?.GetValue<string>();
    string? username = subsonicNode?[nameof(SubsonicConfig.Username)]?.GetValue<string>();
    string? plaintextSubsonicPassword = subsonicNode?[nameof(SubsonicConfig.Password)]?.GetValue<string>();

    if (!string.IsNullOrEmpty(username)) {
      CredentialLookup lookup = await InspectCredentialAsync(CredentialTargets.Subsonic, username, cancellationToken);
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

  private async Task<ServiceAuthStatus> GetYouTubeMusicStatusAsync(CancellationToken cancellationToken) {
    CredentialLookup cookieLookup = await InspectCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, cancellationToken);
    if (cookieLookup.Error != null) {
      return new ServiceAuthStatus("YouTube Music", AuthStatusState.Error, CredentialStorageKind.None, $"Keyring error: {cookieLookup.Error}");
    }

    CredentialLookup tokenLookup = await InspectCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, cancellationToken);
    if (tokenLookup.Error != null) {
      return new ServiceAuthStatus("YouTube Music", AuthStatusState.Error, CredentialStorageKind.None, $"Keyring error: {tokenLookup.Error}");
    }

    if (!string.IsNullOrEmpty(cookieLookup.Value)) {
      bool hasTokens = !string.IsNullOrEmpty(tokenLookup.Value);
      string tokenDetail = hasTokens ? "Tokens present" : "Tokens missing; run 'smoc auth import-cookies'";
      AuthStatusState status = hasTokens ? AuthStatusState.Authenticated : AuthStatusState.CookiesStored;
      return new ServiceAuthStatus("YouTube Music", status, cookieLookup.Storage, $"Cookies stored ({tokenDetail})");
    }

    if (File.Exists(_cookiesPath)) {
      return new ServiceAuthStatus("YouTube Music", AuthStatusState.ConfiguredInsecure, CredentialStorageKind.PlaintextFile, "Plaintext cookies.txt found (Run 'smoc auth migrate')");
    }

    return new ServiceAuthStatus("YouTube Music", AuthStatusState.NotConfigured, CredentialStorageKind.None, "No cookies stored");
  }

  private async Task<ServiceAuthStatus> GetSoundCloudStatusAsync(JsonObject? rootNode, string? configError, CancellationToken cancellationToken) {
    if (configError != null) {
      return new ServiceAuthStatus("SoundCloud", AuthStatusState.Error, CredentialStorageKind.None, $"Config error: {configError}");
    }

    JsonObject? soundCloudNode = rootNode?[nameof(SoundCloudConfig)] as JsonObject;
    string? plaintextAuthToken = soundCloudNode?[nameof(SoundCloudConfig.AuthToken)]?.GetValue<string>();

    CredentialLookup lookup = await InspectCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, cancellationToken);
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

  private static JsonObject GetOrAddObject(JsonObject parent, string propertyName) {
    if (parent[propertyName] is JsonObject existing) {
      return existing;
    }
    JsonObject newObj = new();
    parent[propertyName] = newObj;
    return newObj;
  }
}
