using System.CommandLine;
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
    Lazy<ICredentialStore> credentialStore = new(CredentialStoreFactory.CreateDefault);

    RootCommand rootCommand = new RootCommand("Streaming Music over Console (SMoC)");

    rootCommand.Subcommands.Add(AuthCommand.Create(
        () => new AuthService(credentialStore.Value, _configPath, _cookiesPath, _tokensPath)));

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
      IStreamingClient streamingClient = await CreateStreamingClientAsync(credentialStore.Value, cancellationToken);
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
      if (credentialStore.IsValueCreated && credentialStore.Value is IDisposable disposableStore) {
        disposableStore.Dispose();
      }
      Logging.Information("SMoC exiting...");
    }
  }

  /// <summary>
  /// Creates a streaming client based on the configured active service asynchronously.
  /// </summary>
  /// <param name="credentialStore">Credential store to resolve credentials.</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>An initialized <see cref="IStreamingClient"/>.</returns>
  private static async Task<IStreamingClient> CreateStreamingClientAsync(ICredentialStore credentialStore, CancellationToken cancellationToken = default) {
    long? songCacheSize = SmocConfiguration.Defaults.SongCacheSizeBytes != 0 ? SmocConfiguration.Defaults.SongCacheSizeBytes : null;
    long? artCacheSize = SmocConfiguration.Defaults.AlbumCoverCacheSizeBytes != 0 ? SmocConfiguration.Defaults.AlbumCoverCacheSizeBytes : null;
    int? songCacheMaxElements = SmocConfiguration.Defaults.SongCacheMaxElements != 0 ? SmocConfiguration.Defaults.SongCacheMaxElements : null;
    int? artCacheMaxElements = SmocConfiguration.Defaults.AlbumCoverCacheMaxElements != 0 ? SmocConfiguration.Defaults.AlbumCoverCacheMaxElements : null;
    var songCacheConfig = new CacheConfig(MaxSizeBytes: songCacheSize, MaxElements: songCacheMaxElements);
    var artCacheConfig = new CacheConfig(MaxSizeBytes: artCacheSize, MaxElements: artCacheMaxElements);

    var songCache = new TempFileCacheService("songs", songCacheConfig);
    var artCache = new TempFileCacheService("art", artCacheConfig);

    switch (SmocConfiguration.Defaults.ActiveService) {
      case StreamingService.Subsonic:
        Logging.Information("Resolving Subsonic credentials...");
        var subsonicCreds = await ServiceCredentialResolver.ResolveSubsonicAsync(credentialStore, cancellationToken: cancellationToken);
        Logging.Information("Creating Subsonic streaming client...");
        return SubsonicStreamingClient.Create(songCache, artCache, subsonicCreds);

      case StreamingService.SoundCloud:
        Logging.Information("Resolving SoundCloud credentials...");
        var soundCloudCreds = await ServiceCredentialResolver.ResolveSoundCloudAsync(credentialStore, cancellationToken: cancellationToken);
        Logging.Information("Creating SoundCloud streaming client...");
        return SoundCloudStreamingClient.Create(songCache, artCache, soundCloudCreds);

      case StreamingService.YouTubeMusic:
        Logging.Information("Resolving YouTube Music credentials...");
        var ytmSession = await ServiceCredentialResolver.ResolveYouTubeMusicAsync(credentialStore, _cookiesPath, _tokensPath, cancellationToken: cancellationToken);
        if (ytmSession != null) {
          Logging.Information("Creating authenticated YouTube Music streaming client...");
          return YtmStreamingClient.Create(ytmSession.Cookies, ytmSession.Tokens, songCache, artCache);
        }

        Logging.Information("Cookies or tokens not found. Creating unauthenticated YouTube Music client.");
        return YtmStreamingClient.Create(songCache, artCache);

      default:
        throw new InvalidOperationException($"Unknown streaming service: {SmocConfiguration.Defaults.ActiveService}");
    }
  }
}
