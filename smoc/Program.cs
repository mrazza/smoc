using System.Collections;
using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Smoc.Configuration;
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
  private static readonly string _configPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.config/smoc/config.json";
  private static readonly string _cookiesPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.config/smoc/cookies.txt";
  private static readonly string _tokensPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.config/smoc/tokens.json";
  private static readonly string _logPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.config/smoc/log_{Date}.txt";
  public static readonly string ProductName = "SMoC";

  /// <summary>
  /// The main entry point for the application.
  /// </summary>
  public static async Task Main(string[] args) {
    RootCommand rootCommand = new RootCommand("Streaming Music over Console (SMoC)");

    Command generateTokensCommand = new Command("--gentokens", "Generate PO Token and Visitor Token for YouTube Music.");
    rootCommand.Subcommands.Add(generateTokensCommand);
    generateTokensCommand.SetAction(async (_) => {
      var cookies = YtmStreamingClient.GetCookiesFromFile(_cookiesPath);
      var tokens = await YtmStreamingClient.GenerateTokensAsync(cookies);
      File.WriteAllText(_tokensPath, JsonSerializer.Serialize(tokens));
    });

    rootCommand.SetAction((_) => {
      if (File.Exists(_configPath)) {
        var configFile = File.ReadAllText(_configPath);
        ConfigurationManager.RuntimeConfig = configFile;
      }

      ConfigurationManager.Enable(ConfigLocations.AppResources | ConfigLocations.Runtime);
      Application.SetDefaultKeyBinding(Terminal.Gui.Input.Command.Quit, new Terminal.Gui.Input.PlatformKeyBinding() {
        All = [new Terminal.Gui.Input.Key(':')]
      });

      using IApplication application = Application.Create().Init();

      using ILoggerFactory factory = LoggerFactory.Create(
          builder => builder.SetMinimumLevel(SmocConfiguration.LogLevel).AddFile(_logPath, SmocConfiguration.LogLevel, retainedFileCountLimit: 2));
      ILogger logger = factory.CreateLogger("SMoC");
      Logging.Logger = logger;
      logger.LogInformation("SMoC starting...");

      application.Mouse.IsMouseDisabled = true;
      VimKeyBindings.AddNavigationKeyBindings(application.Keyboard.KeyBindings);
      IStreamingClient streamingClient = CreateStreamingClient();
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
  /// Creates a streaming client based on the configured active service.
  /// </summary>
  /// <returns>An initialized <see cref="IStreamingClient"/>.</returns>
  private static IStreamingClient CreateStreamingClient() {
    long? songCacheSize = SmocConfiguration.SongCacheSizeBytes != 0 ? SmocConfiguration.SongCacheSizeBytes : null;
    long? artCacheSize = SmocConfiguration.AlbumCoverCacheSizeBytes != 0 ? SmocConfiguration.AlbumCoverCacheSizeBytes : null;
    int? songCacheMaxElements = SmocConfiguration.SongCacheMaxElements != 0 ? SmocConfiguration.SongCacheMaxElements : null;
    int? artCacheMaxElements = SmocConfiguration.AlbumCoverCacheMaxElements != 0 ? SmocConfiguration.AlbumCoverCacheMaxElements : null;
    var songCacheConfig = new CacheConfig(MaxSizeBytes: songCacheSize, MaxElements: songCacheMaxElements);
    var artCacheConfig = new CacheConfig(MaxSizeBytes: artCacheSize, MaxElements: artCacheMaxElements);

    var songCache = new TempFileCacheService("songs", songCacheConfig);
    var artCache = new TempFileCacheService("art", artCacheConfig);

    switch (SmocConfiguration.ActiveService) {
      case StreamingService.Subsonic:
        Logging.Information("Creating Subsonic streaming client...");
        return SubsonicStreamingClient.Create(songCache, artCache);

      case StreamingService.SoundCloud:
        Logging.Information("Creating SoundCloud streaming client...");
        return SoundCloudStreamingClient.Create(songCache, artCache);

      case StreamingService.YouTubeMusic:
        Logging.Information("Creating YouTube Music streaming client...");
        if (!File.Exists(_cookiesPath) || !File.Exists(_tokensPath)) {
          Logging.Information("Cookies or tokens not found. Creating new YTM client without authentication.");
          return YtmStreamingClient.Create(songCache, artCache);
        }

        Logging.Information("Cookies and tokens found. Creating new YTM client with authentication.");
        var cookies = YtmStreamingClient.GetCookiesFromFile(_cookiesPath);
        var tokens = JsonSerializer.Deserialize<YtmTokens>(File.ReadAllText(_tokensPath));
        return YtmStreamingClient.Create(cookies, tokens!, songCache, artCache);

      default:
        throw new InvalidOperationException($"Unknown streaming service: {SmocConfiguration.ActiveService}");
    }
  }
}