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
  internal static readonly string DefaultConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "smoc");
  internal static readonly string DefaultConfigPath = Path.Combine(DefaultConfigDir, "config.json");
  internal static readonly string DefaultCookiesPath = Path.Combine(DefaultConfigDir, "cookies.txt");
  internal static readonly string DefaultTokensPath = Path.Combine(DefaultConfigDir, "tokens.json");

  private readonly ICredentialStore _credentialStore;
  private readonly string _configPath;
  private readonly string _cookiesPath;
  private readonly string _tokensPath;
  private readonly Func<List<Cookie>, Task<YtmTokens>> _tokenGenerator;
  private readonly HttpClient _httpClient;

  /// <summary>
  /// Initializes a new instance of <see cref="AuthService"/> with default user profile paths.
  /// </summary>
  public AuthService() : this(
    CredentialStoreFactory.CreateDefault(),
    DefaultConfigPath,
    DefaultCookiesPath,
    DefaultTokensPath,
    YtmStreamingClient.GenerateTokensAsync,
    new HttpClient()
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
    _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
    _cookiesPath = cookiesPath ?? throw new ArgumentNullException(nameof(cookiesPath));
    _tokensPath = tokensPath ?? throw new ArgumentNullException(nameof(tokensPath));
    _tokenGenerator = tokenGenerator ?? YtmStreamingClient.GenerateTokensAsync;
    _httpClient = httpClient ?? new HttpClient();
  }

  /// <inheritdoc />
  public async Task<bool> LoginSubsonicAsync(
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

    await UpdateConfigFileAsync(rootNode => {
      JsonObject subsonicNode = GetOrAddObject(rootNode, nameof(SubsonicConfig));
      subsonicNode[nameof(SubsonicConfig.ServerHost)] = host;
      subsonicNode[nameof(SubsonicConfig.ServerPort)] = port;
      subsonicNode[nameof(SubsonicConfig.ServerScheme)] = scheme;
      subsonicNode[nameof(SubsonicConfig.Username)] = username;
      subsonicNode[nameof(SubsonicConfig.UseToken)] = useToken;
      subsonicNode.Remove(nameof(SubsonicConfig.Password));
    }, cancellationToken);

    return true;
  }

  /// <inheritdoc />
  public async Task<bool> LoginSoundCloudAsync(
    string clientId,
    string authToken,
    CancellationToken cancellationToken = default
  ) {
    ArgumentException.ThrowIfNullOrWhiteSpace(authToken);

    await _credentialStore.SetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, authToken, cancellationToken);

    if (!string.IsNullOrWhiteSpace(clientId)) {
      await _credentialStore.SetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountClientId, clientId, cancellationToken);
    }

    await UpdateConfigFileAsync(rootNode => {
      JsonObject soundCloudNode = GetOrAddObject(rootNode, nameof(SoundCloudConfig));
      if (!string.IsNullOrWhiteSpace(clientId)) {
        soundCloudNode[nameof(SoundCloudConfig.ClientId)] = clientId;
      }
      soundCloudNode.Remove(nameof(SoundCloudConfig.AuthToken));
    }, cancellationToken);

    return true;
  }

  /// <inheritdoc />
  public async Task<ImportCookiesResult> ImportCookiesAsync(
    string? cookieFilePath = null,
    bool deleteSource = false,
    CancellationToken cancellationToken = default
  ) {
    string targetPath = string.IsNullOrWhiteSpace(cookieFilePath) ? _cookiesPath : cookieFilePath;
    if (!File.Exists(targetPath)) {
      return new ImportCookiesResult(false, $"Cookies file not found: {targetPath}", 0, false, false);
    }

    string cookieContent = await File.ReadAllTextAsync(targetPath, cancellationToken);
    if (string.IsNullOrWhiteSpace(cookieContent)) {
      return new ImportCookiesResult(false, "Cookies file is empty.", 0, false, false);
    }

    List<Cookie> parsedCookies = YtmStreamingClient.ParseCookies(cookieContent);
    if (parsedCookies.Count == 0) {
      return new ImportCookiesResult(false, "No valid cookies found in file.", 0, false, false);
    }

    await _credentialStore.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, cookieContent.Trim(), cancellationToken);

    bool tokensGenerated = false;
    try {
      YtmTokens tokens = await _tokenGenerator(parsedCookies);
      string serializedTokens = JsonSerializer.Serialize(tokens);
      await _credentialStore.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, serializedTokens, cancellationToken);
      tokensGenerated = true;
    } catch (Exception ex) {
      return new ImportCookiesResult(false, $"Cookies stored, but token generation failed: {ex.Message}", parsedCookies.Count, false, false);
    }

    bool sourceDeleted = false;
    if (deleteSource) {
      try {
        File.Delete(targetPath);
        sourceDeleted = true;
      } catch {
        // Ignored; deleteSource failure does not invalidate successful import
      }
    }

    return new ImportCookiesResult(true, "Cookies and tokens stored securely.", parsedCookies.Count, tokensGenerated, sourceDeleted);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<ServiceAuthStatus>> GetStatusAsync(CancellationToken cancellationToken = default) {
    JsonNode? rootNode = await LoadConfigNodeAsync(cancellationToken);
    return [
      await GetSubsonicStatusAsync(rootNode, cancellationToken),
      await GetYouTubeMusicStatusAsync(cancellationToken),
      await GetSoundCloudStatusAsync(rootNode, cancellationToken)
    ];
  }

  private async Task<ServiceAuthStatus> GetSubsonicStatusAsync(JsonNode? rootNode, CancellationToken cancellationToken) {
    JsonObject? subsonicNode = rootNode?[nameof(SubsonicConfig)] as JsonObject;
    string? host = subsonicNode?[nameof(SubsonicConfig.ServerHost)]?.GetValue<string>();
    string? username = subsonicNode?[nameof(SubsonicConfig.Username)]?.GetValue<string>();
    string? plaintextSubsonicPassword = subsonicNode?[nameof(SubsonicConfig.Password)]?.GetValue<string>();

    if (!string.IsNullOrEmpty(username)) {
      string? keyringPassword = await TryGetCredentialAsync(CredentialTargets.Subsonic, username, cancellationToken);
      if (!string.IsNullOrEmpty(keyringPassword)) {
        return new ServiceAuthStatus("Subsonic", "Authenticated", "Keyring", $"{username} @ {host ?? "unspecified"}");
      }
      if (!string.IsNullOrEmpty(plaintextSubsonicPassword)) {
        return new ServiceAuthStatus("Subsonic", "Configured (Insecure)", "Plaintext Config", $"{username} @ {host ?? "unspecified"} (Run 'smoc auth migrate')");
      }
      return new ServiceAuthStatus("Subsonic", "Missing Credentials", "None", $"{username} @ {host ?? "unspecified"} (Password missing)");
    }

    return new ServiceAuthStatus("Subsonic", "Not Configured", "None", "No username/host configured in config.json");
  }

  private async Task<ServiceAuthStatus> GetYouTubeMusicStatusAsync(CancellationToken cancellationToken) {
    string? ytmCookies = await TryGetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, cancellationToken);
    string? ytmTokens = await TryGetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, cancellationToken);

    if (!string.IsNullOrEmpty(ytmCookies)) {
      if (!string.IsNullOrEmpty(ytmTokens)) {
        return new ServiceAuthStatus("YouTube Music", "Authenticated", "Keyring", "Cookies stored (Tokens valid)");
      }
      return new ServiceAuthStatus("YouTube Music", "Cookies Stored", "Keyring", "Cookies stored (Tokens missing; run 'smoc auth import-cookies')");
    }
    if (File.Exists(_cookiesPath)) {
      return new ServiceAuthStatus("YouTube Music", "Configured (Insecure)", "Plaintext File", "Plaintext cookies.txt found (Run 'smoc auth migrate')");
    }
    return new ServiceAuthStatus("YouTube Music", "Not Configured", "None", "No cookies stored");
  }

  private async Task<ServiceAuthStatus> GetSoundCloudStatusAsync(JsonNode? rootNode, CancellationToken cancellationToken) {
    JsonObject? soundCloudNode = rootNode?[nameof(SoundCloudConfig)] as JsonObject;
    string? configClientId = soundCloudNode?[nameof(SoundCloudConfig.ClientId)]?.GetValue<string>();
    string? configAuthToken = soundCloudNode?[nameof(SoundCloudConfig.AuthToken)]?.GetValue<string>();

    string? scToken = await TryGetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, cancellationToken);
    string? scClientId = await TryGetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountClientId, cancellationToken);
    string effectiveClientId = scClientId ?? configClientId ?? "default";

    if (!string.IsNullOrEmpty(scToken)) {
      return new ServiceAuthStatus("SoundCloud", "Configured", "Keyring", $"Token stored (ClientId: {effectiveClientId})");
    }
    if (!string.IsNullOrEmpty(configAuthToken)) {
      return new ServiceAuthStatus("SoundCloud", "Configured (Insecure)", "Plaintext Config", $"Plaintext AuthToken in config.json (Run 'smoc auth migrate')");
    }
    return new ServiceAuthStatus("SoundCloud", "Not Configured", "None", "No token configured");
  }

  /// <inheritdoc />
  public async Task<bool> LogoutAsync(string serviceName, CancellationToken cancellationToken = default) {
    ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

    string normalized = serviceName.Trim().ToLowerInvariant();
    bool anyRemoved = false;

    if (normalized is "subsonic" or "all") {
      JsonNode? rootNode = await LoadConfigNodeAsync(cancellationToken);
      string? username = rootNode?[nameof(SubsonicConfig)]?[nameof(SubsonicConfig.Username)]?.GetValue<string>();
      if (!string.IsNullOrEmpty(username)) {
        await _credentialStore.DeleteCredentialAsync(CredentialTargets.Subsonic, username, cancellationToken);
        anyRemoved = true;
      }
    }

    if (normalized is "youtubemusic" or "youtube" or "ytm" or "all") {
      await _credentialStore.DeleteCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, cancellationToken);
      await _credentialStore.DeleteCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, cancellationToken);
      anyRemoved = true;
    }

    if (normalized is "soundcloud" or "all") {
      await _credentialStore.DeleteCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, cancellationToken);
      await _credentialStore.DeleteCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountClientId, cancellationToken);
      anyRemoved = true;
    }

    return anyRemoved;
  }

  /// <inheritdoc />
  public async Task<MigrationResult> MigrateAsync(bool backupPlaintextFiles = true, CancellationToken cancellationToken = default) {
    List<string> migratedItems = [];
    List<string> cleanedFiles = [];
    List<string> errors = [];

    JsonNode? rootNode = await LoadConfigNodeAsync(cancellationToken);
    bool subsonicModified = await MigrateSubsonicAsync(rootNode, migratedItems, errors, cancellationToken);
    bool soundCloudModified = await MigrateSoundCloudAsync(rootNode, migratedItems, errors, cancellationToken);

    if ((subsonicModified || soundCloudModified) && rootNode != null) {
      try {
        await WriteConfigNodeAsync(rootNode, cancellationToken);
        cleanedFiles.Add(_configPath);
      } catch (Exception ex) {
        errors.Add($"Failed to sanitize config.json: {ex.Message}");
      }
    }

    await MigrateYouTubeMusicAsync(backupPlaintextFiles, migratedItems, cleanedFiles, errors, cancellationToken);

    return new MigrationResult(errors.Count == 0, migratedItems, cleanedFiles, errors);
  }

  private async Task<bool> MigrateSubsonicAsync(
    JsonNode? rootNode,
    List<string> migratedItems,
    List<string> errors,
    CancellationToken cancellationToken
  ) {
    if (rootNode?[nameof(SubsonicConfig)] is not JsonObject subsonicNode) {
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
    JsonNode? rootNode,
    List<string> migratedItems,
    List<string> errors,
    CancellationToken cancellationToken
  ) {
    if (rootNode?[nameof(SoundCloudConfig)] is not JsonObject soundCloudNode) {
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
    List<string> queryParams = [
      $"u={Uri.EscapeDataString(username)}",
      "v=1.16.1",
      "c=smoc",
      "f=json"
    ];

    if (useToken) {
      var (token, salt) = SubsonicAuthentication.GenerateToken(password);
      queryParams.Add($"t={token}");
      queryParams.Add($"s={salt}");
    } else {
      queryParams.Add($"p={Uri.EscapeDataString(password)}");
    }

    UriBuilder builder = new(scheme, host, port, "rest/ping.view") {
      Query = string.Join("&", queryParams)
    };

    HttpResponseMessage response = await _httpClient.GetAsync(builder.Uri, cancellationToken);
    response.EnsureSuccessStatusCode();

    string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
    using JsonDocument doc = JsonDocument.Parse(responseBody);
    if (doc.RootElement.TryGetProperty("subsonic-response", out JsonElement respElement)) {
      if (respElement.TryGetProperty("status", out JsonElement status) && status.GetString() == "failed") {
        string errorMsg = "Unknown Subsonic error";
        if (respElement.TryGetProperty("error", out JsonElement error) && error.TryGetProperty("message", out JsonElement msg)) {
          errorMsg = msg.GetString() ?? errorMsg;
        }
        throw new InvalidOperationException($"Subsonic server returned error: {errorMsg}");
      }
    }
  }

  private async Task<string?> TryGetCredentialAsync(string service, string account, CancellationToken cancellationToken) {
    try {
      return await _credentialStore.GetCredentialAsync(service, account, cancellationToken);
    } catch {
      return null;
    }
  }

  private async Task<JsonNode?> LoadConfigNodeAsync(CancellationToken cancellationToken) {
    if (!File.Exists(_configPath)) {
      return null;
    }

    string content = await File.ReadAllTextAsync(_configPath, cancellationToken);
    if (string.IsNullOrWhiteSpace(content)) {
      return new JsonObject();
    }

    try {
      return JsonNode.Parse(content);
    } catch {
      return new JsonObject();
    }
  }

  private async Task WriteConfigNodeAsync(JsonNode node, CancellationToken cancellationToken) {
    string? dir = Path.GetDirectoryName(_configPath);
    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
      Directory.CreateDirectory(dir);
    }

    JsonSerializerOptions options = new() { WriteIndented = true };
    string serialized = node.ToJsonString(options);
    await File.WriteAllTextAsync(_configPath, serialized, cancellationToken);
  }

  private async Task UpdateConfigFileAsync(Action<JsonObject> updater, CancellationToken cancellationToken) {
    JsonNode? rootNode = await LoadConfigNodeAsync(cancellationToken);
    JsonObject jsonObject = rootNode as JsonObject ?? new JsonObject();
    updater(jsonObject);
    await WriteConfigNodeAsync(jsonObject, cancellationToken);
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