using System.Net;
using System.Text.Json.Nodes;
using Smoc.Cli.Auth.SoundCloud;
using Smoc.Cli.Auth.Subsonic;
using Smoc.Cli.Auth.YouTubeMusic;
using Smoc.Configuration;
using Smoc.Security;
using Smoc.Streaming.YouTubeMusic;

namespace Smoc.Cli.Auth;

/// <summary>
/// Provides streaming service authentication management, credential migration, and status reporting.
/// </summary>
public class AuthService : IAuthService, IDisposable {
  private readonly ICredentialStore _credentialStore;
  private readonly ConfigFileWriter _configWriter;
  private readonly HttpClient _httpClient;
  private readonly bool _ownsHttpClient;
  private readonly SubsonicAuthHandler _subsonicHandler;
  private readonly SoundCloudAuthHandler _soundCloudHandler;
  private readonly YouTubeMusicAuthHandler _youTubeMusicHandler;
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
    _credentialStore = credentialStore;
    _configWriter = new ConfigFileWriter(configPath);
    _ownsHttpClient = httpClient == null;
    _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

    _subsonicHandler = new SubsonicAuthHandler(_credentialStore, _configWriter, _httpClient);
    _soundCloudHandler = new SoundCloudAuthHandler(_credentialStore, _configWriter);
    _youTubeMusicHandler = new YouTubeMusicAuthHandler(
        _credentialStore,
        cookiesPath,
        tokensPath,
        tokenGenerator ?? YtmStreamingClient.GenerateTokensAsync);
  }

  /// <summary>
  /// Gets the handler for Subsonic authentication operations.
  /// </summary>
  public SubsonicAuthHandler Subsonic => _subsonicHandler;

  /// <summary>
  /// Gets the handler for SoundCloud authentication operations.
  /// </summary>
  public SoundCloudAuthHandler SoundCloud => _soundCloudHandler;

  /// <summary>
  /// Gets the handler for YouTube Music authentication operations.
  /// </summary>
  public YouTubeMusicAuthHandler YouTubeMusic => _youTubeMusicHandler;

  /// <inheritdoc />
  public Task LoginSubsonicAsync(
    string host,
    int port,
    string scheme,
    string username,
    string password,
    bool useToken,
    bool verifyConnection = true,
    CancellationToken cancellationToken = default
  ) => _subsonicHandler.LoginAsync(host, port, scheme, username, password, useToken, verifyConnection, cancellationToken);

  /// <inheritdoc />
  public Task LoginSoundCloudAsync(
    string clientId,
    string authToken,
    CancellationToken cancellationToken = default
  ) => _soundCloudHandler.LoginAsync(clientId, authToken, cancellationToken);

  /// <inheritdoc />
  public Task<ImportCookiesResult> ImportCookiesAsync(
    string? cookieFilePath = null,
    bool deleteSource = false,
    CancellationToken cancellationToken = default
  ) => _youTubeMusicHandler.ImportCookiesAsync(cookieFilePath, deleteSource, cancellationToken);

  /// <inheritdoc />
  public Task GenerateTokensAsync(CancellationToken cancellationToken = default) =>
      _youTubeMusicHandler.GenerateTokensAsync(cancellationToken);

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
      await _subsonicHandler.GetStatusAsync(rootNode, configError, cancellationToken),
      await _youTubeMusicHandler.GetStatusAsync(cancellationToken),
      await _soundCloudHandler.GetStatusAsync(rootNode, configError, cancellationToken)
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
      anyRemoved |= await _subsonicHandler.LogoutAsync(rootNode, cancellationToken);
    }

    if (target is AuthServiceTarget.YouTubeMusic or AuthServiceTarget.All) {
      anyRemoved |= await _youTubeMusicHandler.LogoutAsync(cancellationToken);
    }

    if (target is AuthServiceTarget.SoundCloud or AuthServiceTarget.All) {
      anyRemoved |= await _soundCloudHandler.LogoutAsync(cancellationToken);
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
      bool subsonicModified = await _subsonicHandler.MigrateAsync(rootNode, migratedItems, errors, cancellationToken);
      bool soundCloudModified = await _soundCloudHandler.MigrateAsync(rootNode, migratedItems, errors, cancellationToken);

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

    await _youTubeMusicHandler.MigrateAsync(backupPlaintextFiles, migratedItems, cleanedFiles, errors, cancellationToken);

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
}
