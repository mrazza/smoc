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
public class AuthService : IAuthService {
  private readonly ICredentialStore _credentialStore;
  private readonly ConfigFileWriter _configWriter;
  private readonly string _cookiesPath;
  private readonly string _tokensPath;
  private readonly Func<List<Cookie>, Task<YtmTokens>> _tokenGenerator;
  private readonly HttpClient _httpClient;

  /// <summary>
  /// Initializes a new instance of <see cref="AuthService"/> with default user profile paths.
  /// </summary>
  public AuthService() : this(
    CredentialStoreFactory.CreateDefault(),
    SmocPaths.DefaultConfigPath,
    SmocPaths.DefaultCookiesPath,
    SmocPaths.DefaultTokensPath,
    YtmStreamingClient.GenerateTokensAsync,
    new HttpClient { Timeout = TimeSpan.FromSeconds(10) }
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
    }, backupOriginal: true, cancellationToken);
  }

  /// <inheritdoc />
  public async Task LoginSoundCloudAsync(
    string clientId,
    string authToken,
    CancellationToken cancellationToken = default
  ) {
    ArgumentException.ThrowIfNullOrWhiteSpace(authToken);

    await _credentialStore.SetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, authToken, cancellationToken);

    if (!string.IsNullOrWhiteSpace(clientId)) {
      await _credentialStore.SetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountClientId, clientId, cancellationToken);
    }

    await _configWriter.UpdateAsync(rootNode => {
      JsonObject soundCloudNode = GetOrAddObject(rootNode, nameof(SoundCloudConfig));
      if (!string.IsNullOrWhiteSpace(clientId)) {
        soundCloudNode[nameof(SoundCloudConfig.ClientId)] = clientId;
      }
      soundCloudNode.Remove(nameof(SoundCloudConfig.AuthToken));
    }, backupOriginal: true, cancellationToken);
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
        } catch (Exception ex) {
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

    string normalized = serviceName.Trim().ToLowerInvariant();
    if (normalized is not ("subsonic" or "soundcloud" or "youtube" or "youtubemusic" or "ytm" or "all")) {
      throw new ArgumentException($"Unknown service '{serviceName}'. Supported services are: subsonic, soundcloud, youtube, all.", nameof(serviceName));
    }

    bool anyRemoved = false;

    if (normalized is "subsonic" or "all") {
      JsonObject? rootNode = null;
      try {
        rootNode = await _configWriter.LoadAsync(cancellationToken);
      } catch {
        // Best effort config load
      }
      string? username = rootNode?[nameof(SubsonicConfig)]?[nameof(SubsonicConfig.Username)]?.GetValue<string>();
      if (!string.IsNullOrEmpty(username)) {
        string? existing = await TryGetRawCredentialAsync(CredentialTargets.Subsonic, username, cancellationToken);
        if (existing != null) {
          await _credentialStore.DeleteCredentialAsync(CredentialTargets.Subsonic, username, cancellationToken);
          anyRemoved = true;
        }
      }
    }

    if (normalized is "youtubemusic" or "youtube" or "ytm" or "all") {
      string? cookies = await TryGetRawCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, cancellationToken);
      if (cookies != null) {
        await _credentialStore.DeleteCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, cancellationToken);
        anyRemoved = true;
      }
      string? tokens = await TryGetRawCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, cancellationToken);
      if (tokens != null) {
        await _credentialStore.DeleteCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, cancellationToken);
        anyRemoved = true;
      }
    }

    if (normalized is "soundcloud" or "all") {
      string? token = await TryGetRawCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, cancellationToken);
      if (token != null) {
        await _credentialStore.DeleteCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, cancellationToken);
        anyRemoved = true;
      }
      string? clientId = await TryGetRawCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountClientId, cancellationToken);
      if (clientId != null) {
        await _credentialStore.DeleteCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountClientId, cancellationToken);
        anyRemoved = true;
      }
    }

    return anyRemoved;
  }

  /// <inheritdoc />
  public async Task<MigrationResult> MigrateAsync(bool backupPlaintextFiles = true, CancellationToken cancellationToken = default) {
    List<string> migratedItems = [];
    List<string> cleanedFiles = [];
    List<string> errors = [];

    JsonObject? rootNode = null;
    try {
      rootNode = await _configWriter.LoadAsync(cancellationToken);
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
        } catch (Exception ex) {
          errors.Add($"Failed to sanitize config.json: {ex.Message}");
        }
      }
    }

    await MigrateYouTubeMusicAsync(backupPlaintextFiles, migratedItems, cleanedFiles, errors, cancellationToken);

    return new MigrationResult(errors.Count == 0, migratedItems, cleanedFiles, errors);
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
    string? clientId = soundCloudNode[nameof(SoundCloudConfig.ClientId)]?.GetValue<string>();

    if (string.IsNullOrEmpty(authToken)) {
      return false;
    }

    try {
      await _credentialStore.SetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, authToken, cancellationToken);
      if (!string.IsNullOrEmpty(clientId)) {
        await _credentialStore.SetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountClientId, clientId, cancellationToken);
      }
      soundCloudNode.Remove(nameof(SoundCloudConfig.AuthToken));
      migratedItems.Add("SoundCloud authentication token");
      return true;
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

  private sealed record CredentialLookup(string? Value, string Storage, string? Error);

  private async Task<CredentialLookup> InspectCredentialAsync(string service, string account, CancellationToken cancellationToken) {
    if (_credentialStore is CompositeCredentialStore composite) {
      string? firstError = null;
      foreach (ICredentialStore store in composite.Stores) {
        try {
          string? value = await store.GetCredentialAsync(service, account, cancellationToken);
          if (value != null) {
            string storage = store is EnvironmentCredentialStore ? "Environment" : "Keyring";
            return new CredentialLookup(value, storage, null);
          }
        } catch (Exception ex) {
          firstError ??= ex.Message;
        }
      }
      return new CredentialLookup(null, "None", firstError);
    }

    try {
      string? value = await _credentialStore.GetCredentialAsync(service, account, cancellationToken);
      if (value != null) {
        string storage = _credentialStore is EnvironmentCredentialStore ? "Environment" : "Keyring";
        return new CredentialLookup(value, storage, null);
      }
      return new CredentialLookup(null, "None", null);
    } catch (Exception ex) {
      return new CredentialLookup(null, "None", ex.Message);
    }
  }

  private async Task<string?> TryGetRawCredentialAsync(string service, string account, CancellationToken cancellationToken) {
    try {
      return await _credentialStore.GetCredentialAsync(service, account, cancellationToken);
    } catch {
      return null;
    }
  }

  private async Task<ServiceAuthStatus> GetSubsonicStatusAsync(JsonObject? rootNode, string? configError, CancellationToken cancellationToken) {
    if (configError != null) {
      return new ServiceAuthStatus("Subsonic", "Error", "None", $"Config error: {configError}");
    }

    JsonObject? subsonicNode = rootNode?[nameof(SubsonicConfig)] as JsonObject;
    string? host = subsonicNode?[nameof(SubsonicConfig.ServerHost)]?.GetValue<string>();
    string? username = subsonicNode?[nameof(SubsonicConfig.Username)]?.GetValue<string>();
    string? plaintextSubsonicPassword = subsonicNode?[nameof(SubsonicConfig.Password)]?.GetValue<string>();

    if (!string.IsNullOrEmpty(username)) {
      CredentialLookup lookup = await InspectCredentialAsync(CredentialTargets.Subsonic, username, cancellationToken);
      if (lookup.Error != null) {
        return new ServiceAuthStatus("Subsonic", "Error", "None", $"Keyring error: {lookup.Error}");
      }
      if (!string.IsNullOrEmpty(lookup.Value)) {
        return new ServiceAuthStatus("Subsonic", "Authenticated", lookup.Storage, $"{username} @ {host ?? "unspecified"}");
      }
      if (!string.IsNullOrEmpty(plaintextSubsonicPassword)) {
        return new ServiceAuthStatus("Subsonic", "Configured (Insecure)", "Plaintext Config", $"{username} @ {host ?? "unspecified"} (Run 'smoc auth migrate')");
      }
      return new ServiceAuthStatus("Subsonic", "Missing Credentials", "None", $"{username} @ {host ?? "unspecified"} (Password missing)");
    }

    return new ServiceAuthStatus("Subsonic", "Not Configured", "None", "No username/host configured in config.json");
  }

  private async Task<ServiceAuthStatus> GetYouTubeMusicStatusAsync(CancellationToken cancellationToken) {
    CredentialLookup cookieLookup = await InspectCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, cancellationToken);
    CredentialLookup tokenLookup = await InspectCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, cancellationToken);

    if (cookieLookup.Error != null) {
      return new ServiceAuthStatus("YouTube Music", "Error", "None", $"Keyring error: {cookieLookup.Error}");
    }

    if (!string.IsNullOrEmpty(cookieLookup.Value)) {
      string tokenDetail = !string.IsNullOrEmpty(tokenLookup.Value) ? "Tokens present" : "Tokens missing; run 'smoc auth import-cookies'";
      string status = !string.IsNullOrEmpty(tokenLookup.Value) ? "Authenticated" : "Cookies Stored";
      return new ServiceAuthStatus("YouTube Music", status, cookieLookup.Storage, $"Cookies stored ({tokenDetail})");
    }

    if (File.Exists(_cookiesPath)) {
      return new ServiceAuthStatus("YouTube Music", "Configured (Insecure)", "Plaintext File", "Plaintext cookies.txt found (Run 'smoc auth migrate')");
    }

    return new ServiceAuthStatus("YouTube Music", "Not Configured", "None", "No cookies stored");
  }

  private async Task<ServiceAuthStatus> GetSoundCloudStatusAsync(JsonObject? rootNode, string? configError, CancellationToken cancellationToken) {
    if (configError != null) {
      return new ServiceAuthStatus("SoundCloud", "Error", "None", $"Config error: {configError}");
    }

    JsonObject? soundCloudNode = rootNode?[nameof(SoundCloudConfig)] as JsonObject;
    string? plaintextAuthToken = soundCloudNode?[nameof(SoundCloudConfig.AuthToken)]?.GetValue<string>();

    CredentialLookup lookup = await InspectCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, cancellationToken);
    if (lookup.Error != null) {
      return new ServiceAuthStatus("SoundCloud", "Error", "None", $"Keyring error: {lookup.Error}");
    }
    if (!string.IsNullOrEmpty(lookup.Value)) {
      return new ServiceAuthStatus("SoundCloud", "Authenticated", lookup.Storage, "OAuth token stored");
    }
    if (!string.IsNullOrEmpty(plaintextAuthToken)) {
      return new ServiceAuthStatus("SoundCloud", "Configured (Insecure)", "Plaintext Config", "Plaintext AuthToken found (Run 'smoc auth migrate')");
    }

    return new ServiceAuthStatus("SoundCloud", "Not Configured", "None", "No credentials stored");
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
