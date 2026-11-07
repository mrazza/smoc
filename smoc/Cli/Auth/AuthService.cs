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
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "smoc", "config.json"),
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "smoc", "cookies.txt"),
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "smoc", "tokens.json"),
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
    if (string.IsNullOrWhiteSpace(host)) {
      throw new ArgumentException("Server host cannot be empty.", nameof(host));
    }
    if (string.IsNullOrWhiteSpace(username)) {
      throw new ArgumentException("Username cannot be empty.", nameof(username));
    }
    if (string.IsNullOrWhiteSpace(password)) {
      throw new ArgumentException("Password cannot be empty.", nameof(password));
    }

    if (verifyConnection) {
      await VerifySubsonicConnectionAsync(host, port, scheme, username, password, useToken, cancellationToken);
    }

    await _credentialStore.SetCredentialAsync(CredentialTargets.Subsonic, username, password, cancellationToken);

    await UpdateConfigFileAsync(rootNode => {
      JsonObject subsonicNode = GetOrAddObject(rootNode, "SubsonicConfig");
      subsonicNode["ServerHost"] = host;
      subsonicNode["ServerPort"] = port;
      subsonicNode["ServerScheme"] = scheme;
      subsonicNode["Username"] = username;
      subsonicNode["UseToken"] = useToken;
      subsonicNode.Remove("Password");
    }, cancellationToken);

    return true;
  }

  /// <inheritdoc />
  public async Task<bool> LoginSoundCloudAsync(
    string clientId,
    string authToken,
    CancellationToken cancellationToken = default
  ) {
    if (string.IsNullOrWhiteSpace(authToken)) {
      throw new ArgumentException("Authentication token cannot be empty.", nameof(authToken));
    }

    await _credentialStore.SetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, authToken, cancellationToken);

    if (!string.IsNullOrWhiteSpace(clientId)) {
      await _credentialStore.SetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountClientId, clientId, cancellationToken);
    }

    await UpdateConfigFileAsync(rootNode => {
      JsonObject soundCloudNode = GetOrAddObject(rootNode, "SoundCloudConfig");
      if (!string.IsNullOrWhiteSpace(clientId)) {
        soundCloudNode["ClientId"] = clientId;
      }
      soundCloudNode.Remove("AuthToken");
    }, cancellationToken);

    return true;
  }

  /// <inheritdoc />
  public async Task<ImportCookiesResult> ImportCookiesAsync(
    string cookieFilePath,
    bool deleteSource = false,
    CancellationToken cancellationToken = default
  ) {
    if (!File.Exists(cookieFilePath)) {
      return new ImportCookiesResult(false, $"Cookies file not found: {cookieFilePath}", 0, false, false);
    }

    string cookieContent = await File.ReadAllTextAsync(cookieFilePath, cancellationToken);
    if (string.IsNullOrWhiteSpace(cookieContent)) {
      return new ImportCookiesResult(false, "Cookies file is empty.", 0, false, false);
    }

    List<Cookie> parsedCookies = ParseCookies(cookieContent);
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
        File.Delete(cookieFilePath);
        sourceDeleted = true;
      } catch {
        // Ignored; deleteSource failure does not invalidate successful import
      }
    }

    return new ImportCookiesResult(true, "Cookies and tokens stored securely.", parsedCookies.Count, tokensGenerated, sourceDeleted);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<ServiceAuthStatus>> GetStatusAsync(CancellationToken cancellationToken = default) {
    List<ServiceAuthStatus> statuses = [];

    // 1. Subsonic
    JsonNode? rootNode = await LoadConfigNodeAsync(cancellationToken);
    JsonObject? subsonicNode = rootNode?["SubsonicConfig"] as JsonObject;
    string? host = subsonicNode?["ServerHost"]?.GetValue<string>();
    string? username = subsonicNode?["Username"]?.GetValue<string>();
    string? plaintextSubsonicPassword = subsonicNode?["Password"]?.GetValue<string>();

    if (!string.IsNullOrEmpty(username)) {
      string? keyringPassword = await TryGetCredentialAsync(CredentialTargets.Subsonic, username, cancellationToken);
      if (!string.IsNullOrEmpty(keyringPassword)) {
        statuses.Add(new ServiceAuthStatus("Subsonic", "Authenticated", "Keyring", $"{username} @ {host ?? "unspecified"}"));
      } else if (!string.IsNullOrEmpty(plaintextSubsonicPassword)) {
        statuses.Add(new ServiceAuthStatus("Subsonic", "Configured (Insecure)", "Plaintext Config", $"{username} @ {host ?? "unspecified"} (Run 'smoc auth migrate')"));
      } else {
        statuses.Add(new ServiceAuthStatus("Subsonic", "Missing Credentials", "None", $"{username} @ {host ?? "unspecified"} (Password missing)"));
      }
    } else {
      statuses.Add(new ServiceAuthStatus("Subsonic", "Not Configured", "None", "No username/host configured in config.json"));
    }

    // 2. YouTube Music
    string? ytmCookies = await TryGetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, cancellationToken);
    string? ytmTokens = await TryGetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, cancellationToken);

    if (!string.IsNullOrEmpty(ytmCookies)) {
      if (!string.IsNullOrEmpty(ytmTokens)) {
        statuses.Add(new ServiceAuthStatus("YouTube Music", "Authenticated", "Keyring", "Cookies stored (Tokens valid)"));
      } else {
        statuses.Add(new ServiceAuthStatus("YouTube Music", "Cookies Stored", "Keyring", "Cookies stored (Tokens missing; run 'smoc auth import-cookies')"));
      }
    } else if (File.Exists(_cookiesPath)) {
      statuses.Add(new ServiceAuthStatus("YouTube Music", "Configured (Insecure)", "Plaintext File", "Plaintext cookies.txt found (Run 'smoc auth migrate')"));
    } else {
      statuses.Add(new ServiceAuthStatus("YouTube Music", "Not Configured", "None", "No cookies stored"));
    }

    // 3. SoundCloud
    JsonObject? soundCloudNode = rootNode?["SoundCloudConfig"] as JsonObject;
    string? configClientId = soundCloudNode?["ClientId"]?.GetValue<string>();
    string? configAuthToken = soundCloudNode?["AuthToken"]?.GetValue<string>();

    string? scToken = await TryGetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, cancellationToken);
    string? scClientId = await TryGetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountClientId, cancellationToken);
    string effectiveClientId = scClientId ?? configClientId ?? "default";

    if (!string.IsNullOrEmpty(scToken)) {
      statuses.Add(new ServiceAuthStatus("SoundCloud", "Configured", "Keyring", $"Token stored (ClientId: {effectiveClientId})"));
    } else if (!string.IsNullOrEmpty(configAuthToken)) {
      statuses.Add(new ServiceAuthStatus("SoundCloud", "Configured (Insecure)", "Plaintext Config", $"Plaintext AuthToken in config.json (Run 'smoc auth migrate')"));
    } else {
      statuses.Add(new ServiceAuthStatus("SoundCloud", "Not Configured", "None", "No token configured"));
    }

    return statuses;
  }

  /// <inheritdoc />
  public async Task<bool> LogoutAsync(string serviceName, CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(serviceName)) {
      throw new ArgumentException("Service name cannot be empty.", nameof(serviceName));
    }

    string normalized = serviceName.Trim().ToLowerInvariant();
    bool anyRemoved = false;

    if (normalized is "subsonic" or "all") {
      JsonNode? rootNode = await LoadConfigNodeAsync(cancellationToken);
      string? username = rootNode?["SubsonicConfig"]?["Username"]?.GetValue<string>();
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

    // 1. Migrate Subsonic password from config.json
    JsonNode? rootNode = await LoadConfigNodeAsync(cancellationToken);
    bool configModified = false;

    if (rootNode?["SubsonicConfig"] is JsonObject subsonicNode) {
      string? username = subsonicNode["Username"]?.GetValue<string>();
      string? password = subsonicNode["Password"]?.GetValue<string>();

      if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password)) {
        try {
          await _credentialStore.SetCredentialAsync(CredentialTargets.Subsonic, username, password, cancellationToken);
          subsonicNode.Remove("Password");
          configModified = true;
          migratedItems.Add($"Subsonic password for user '{username}'");
        } catch (Exception ex) {
          errors.Add($"Failed to migrate Subsonic password: {ex.Message}");
        }
      }
    }

    // 2. Migrate SoundCloud token from config.json
    if (rootNode?["SoundCloudConfig"] is JsonObject soundCloudNode) {
      string? authToken = soundCloudNode["AuthToken"]?.GetValue<string>();
      string? clientId = soundCloudNode["ClientId"]?.GetValue<string>();

      if (!string.IsNullOrEmpty(authToken)) {
        try {
          await _credentialStore.SetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, authToken, cancellationToken);
          if (!string.IsNullOrEmpty(clientId)) {
            await _credentialStore.SetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountClientId, clientId, cancellationToken);
          }
          soundCloudNode.Remove("AuthToken");
          configModified = true;
          migratedItems.Add("SoundCloud authentication token");
        } catch (Exception ex) {
          errors.Add($"Failed to migrate SoundCloud token: {ex.Message}");
        }
      }
    }

    if (configModified && rootNode != null) {
      try {
        await WriteConfigNodeAsync(rootNode, cancellationToken);
        cleanedFiles.Add(_configPath);
      } catch (Exception ex) {
        errors.Add($"Failed to sanitize config.json: {ex.Message}");
      }
    }

    // 3. Migrate YouTube Music cookies and tokens
    if (File.Exists(_cookiesPath)) {
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
            List<Cookie> parsedCookies = ParseCookies(cookieContent);
            if (parsedCookies.Count > 0) {
              YtmTokens generated = await _tokenGenerator(parsedCookies);
              await _credentialStore.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, JsonSerializer.Serialize(generated), cancellationToken);
              migratedItems.Add("Generated YouTube Music visitor and PO tokens");
            }
          }

          if (backupPlaintextFiles) {
            string backupCookiePath = _cookiesPath + ".bak";
            File.Move(_cookiesPath, backupCookiePath, overwrite: true);
            cleanedFiles.Add($"{_cookiesPath} (renamed to .bak)");

            if (File.Exists(_tokensPath)) {
              string backupTokensPath = _tokensPath + ".bak";
              File.Move(_tokensPath, backupTokensPath, overwrite: true);
              cleanedFiles.Add($"{_tokensPath} (renamed to .bak)");
            }
          } else {
            File.Delete(_cookiesPath);
            cleanedFiles.Add(_cookiesPath);
            if (File.Exists(_tokensPath)) {
              File.Delete(_tokensPath);
              cleanedFiles.Add(_tokensPath);
            }
          }
        }
      } catch (Exception ex) {
        errors.Add($"Failed to migrate YouTube Music cookies/tokens: {ex.Message}");
      }
    }

    return new MigrationResult(errors.Count == 0, migratedItems, cleanedFiles, errors);
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

  private static List<Cookie> ParseCookies(string cookieString) {
    return cookieString
      .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
      .Select(x => x.Split('=', 2))
      .Where(parts => parts.Length == 2)
      .Select(parts => new Cookie(parts[0].Trim(), parts[1].Trim(), "/", ".youtube.com"))
      .ToList();
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
