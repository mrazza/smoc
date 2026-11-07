using System.Collections;
using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Smoc.Cli.Auth;
using Smoc.Configuration;
using Smoc.Security;
using Smoc.Services.Caching;
using Smoc.Streaming;
using Smoc.Streaming.Subsonic;
using Smoc.Streaming.YouTubeMusic;
using Smoc.Streaming.SoundCloud;
using Smoc.Ui;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;

namespace Smoc;

/// <summary>
/// Trivial entry point for the application that immediately hands off
/// initialization responsibility to the MainWindow.
/// </summary>
public static class Program {
  private static readonly string _configPath = SmocPaths.DefaultConfigPath;
  private static readonly string _cookiesPath = SmocPaths.DefaultCookiesPath;
  private static readonly string _tokensPath = SmocPaths.DefaultTokensPath;
  private static readonly string _logPath = SmocPaths.DefaultLogPath;

  /// <summary>
  /// The product name of the application.
  /// </summary>
  public static readonly string ProductName = "SMoC";

  /// <summary>
  /// The main entry point for the application.
  /// </summary>
  public static async Task Main(string[] args) {
    RootCommand rootCommand = new RootCommand("Streaming Music over Console (SMoC)");

    rootCommand.Subcommands.Add(AuthCommand.Create());

    Command generateTokensCommand = new Command("--gentokens", "Generate PO Token and Visitor Token for YouTube Music.");
    rootCommand.Subcommands.Add(generateTokensCommand);
    generateTokensCommand.SetAction(async (parseResult, cancellationToken) => {
      var credentialStore = CredentialStoreFactory.CreateDefault();
      string? cookiesRaw = null;
      try {
        cookiesRaw = await credentialStore.GetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, cancellationToken);
      } catch { }

      List<System.Net.Cookie>? cookies = null;
      if (!string.IsNullOrWhiteSpace(cookiesRaw)) {
        cookies = YtmStreamingClient.ParseCookies(cookiesRaw);
      } else if (File.Exists(_cookiesPath)) {
        cookies = YtmStreamingClient.GetCookiesFromFile(_cookiesPath);
      }

      if (cookies == null || cookies.Count == 0) {
        throw new InvalidOperationException("No YouTube Music cookies found in Keyring or cookies.txt.");
      }

      var tokens = await YtmStreamingClient.GenerateTokensAsync(cookies);
      if (!string.IsNullOrWhiteSpace(cookiesRaw)) {
        try {
          await credentialStore.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, JsonSerializer.Serialize(tokens), cancellationToken);
        } catch { }
      }
      File.WriteAllText(_tokensPath, JsonSerializer.Serialize(tokens));
    });

    rootCommand.SetAction(async (parseResult, cancellationToken) => {
      var builder = new TuiConfigurationBuilder(ProductName);
      if (File.Exists(_configPath)) {
        var configFile = File.ReadAllText(_configPath);
        builder.RuntimeConfig = configFile;
      }

      builder.BindAppSettings<SmocConfiguration>("SmocConfiguration", s => SmocConfiguration.Defaults = s)
             .BindAppSettings<ListenHistoryConfig>("ListenHistoryConfig", s => ListenHistoryConfig.Defaults = s)
             .BindAppSettings<SoundCloudConfig>("SoundCloudConfig", s => SoundCloudConfig.Defaults = s)
             .BindAppSettings<SubsonicConfig>("SubsonicConfig", s => SubsonicConfig.Defaults = s)
             .BindAppSettings<YouTubeMusicConfig>("YouTubeMusicConfig", s => YouTubeMusicConfig.Defaults = s);

      builder.ApplyToStaticFacades();

      Application.SetDefaultKeyBinding(Terminal.Gui.Input.Command.Quit, new Terminal.Gui.Input.PlatformKeyBinding() {
        All = [new Terminal.Gui.Input.Key(':')]
      });

      using IApplication application = Application.Create().Init();

      using ILoggerFactory factory = LoggerFactory.Create(
          builder => builder.SetMinimumLevel(SmocConfiguration.Defaults.LogLevel).AddFile(_logPath, SmocConfiguration.Defaults.LogLevel, retainedFileCountLimit: 2));
      ILogger logger = factory.CreateLogger("SMoC");
      Logging.Logger = logger;
      logger.LogInformation("SMoC starting...");

      if (!string.IsNullOrEmpty(SubsonicConfig.Defaults.Password) ||
          !string.IsNullOrEmpty(SoundCloudConfig.Defaults.AuthToken) ||
          File.Exists(_cookiesPath)) {
        Console.Error.WriteLine("Notice: Plaintext credentials detected in configuration or cookies. Run 'smoc auth migrate' to securely store credentials.");
        logger.LogWarning("Plaintext credentials detected. Run 'smoc auth migrate' to securely store credentials.");
      }

      application.Mouse.IsMouseDisabled = true;
      VimKeyBindings.AddNavigationKeyBindings(application.Keyboard.KeyBindings);
      IStreamingClient streamingClient = await CreateStreamingClientAsync(cancellationToken: cancellationToken);
      using var window = new MainWindow(streamingClient);
      try {
        application.Run(window, (e) => {
          Logging.Error(e.ToString());
          window.DisplayError(e.Message);
          return true;
        });
      } catch (Exception e) {
        Logging.Error(e.ToString());
        throw;
      }
    });

    try {
      await rootCommand.Parse(args).InvokeAsync();
    } catch (Exception e) {
      Logging.Error(e.ToString());
      throw;
    } finally {
      Logging.Information("SMoC exiting...");
    }
  }

  /// <summary>
  /// Creates a streaming client based on the configured active service asynchronously.
  /// </summary>
  /// <param name="credentialStore">Optional credential store to resolve credentials.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>An initialized <see cref="IStreamingClient"/>.</returns>
  private static async Task<IStreamingClient> CreateStreamingClientAsync(ICredentialStore? credentialStore = null, CancellationToken cancellationToken = default) {
    long? songCacheSize = SmocConfiguration.Defaults.SongCacheSizeBytes != 0 ? SmocConfiguration.Defaults.SongCacheSizeBytes : null;
    long? artCacheSize = SmocConfiguration.Defaults.AlbumCoverCacheSizeBytes != 0 ? SmocConfiguration.Defaults.AlbumCoverCacheSizeBytes : null;
    int? songCacheMaxElements = SmocConfiguration.Defaults.SongCacheMaxElements != 0 ? SmocConfiguration.Defaults.SongCacheMaxElements : null;
    int? artCacheMaxElements = SmocConfiguration.Defaults.AlbumCoverCacheMaxElements != 0 ? SmocConfiguration.Defaults.AlbumCoverCacheMaxElements : null;
    var songCacheConfig = new CacheConfig(MaxSizeBytes: songCacheSize, MaxElements: songCacheMaxElements);
    var artCacheConfig = new CacheConfig(MaxSizeBytes: artCacheSize, MaxElements: artCacheMaxElements);

    var songCache = new TempFileCacheService("songs", songCacheConfig);
    var artCache = new TempFileCacheService("art", artCacheConfig);

    credentialStore ??= CredentialStoreFactory.CreateDefault();

    switch (SmocConfiguration.Defaults.ActiveService) {
      case StreamingService.Subsonic:
        Logging.Information("Creating Subsonic streaming client...");
        return await SubsonicStreamingClient.CreateAsync(songCache, artCache, credentialStore, cancellationToken);

      case StreamingService.SoundCloud:
        Logging.Information("Creating SoundCloud streaming client...");
        return await SoundCloudStreamingClient.CreateAsync(songCache, artCache, credentialStore, cancellationToken);

      case StreamingService.YouTubeMusic:
        Logging.Information("Creating YouTube Music streaming client...");
        return await YtmStreamingClient.CreateAsync(songCache, artCache, credentialStore, _cookiesPath, _tokensPath, cancellationToken: cancellationToken);

      default:
        throw new InvalidOperationException($"Unknown streaming service: {SmocConfiguration.Defaults.ActiveService}");
    }
  }

  /// <summary>
  /// Creates a streaming client based on the configured active service.
  /// </summary>
  /// <returns>An initialized <see cref="IStreamingClient"/>.</returns>
  private static IStreamingClient CreateStreamingClient() => CreateStreamingClientAsync().GetAwaiter().GetResult();
}
