using System.Collections;
using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
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

      // Support flat dotted keys (e.g. { "SmocConfiguration.LogLevel": "Warning" }) from user config files
      BindFlatKeys(builder.Configuration);

      builder.ApplyToStaticFacades();
      AppSchemes.RegisterDefaultSchemes();

      Application.SetDefaultKeyBinding(Terminal.Gui.Input.Command.Quit, new Terminal.Gui.Input.PlatformKeyBinding() {
        All = [new Terminal.Gui.Input.Key(':')]
      });

      using IApplication application = Application.Create().Init();

      using ILoggerFactory factory = LoggerFactory.Create(
          builder => builder.SetMinimumLevel(SmocConfiguration.Defaults.LogLevel).AddFile(_logPath, SmocConfiguration.Defaults.LogLevel, retainedFileCountLimit: 2));
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
  /// Binds flat dotted configuration keys to the settings facades for backwards compatibility.
  /// </summary>
  public static void BindFlatKeys(IConfiguration config) {
    if (Enum.TryParse<LogLevel>(config["SmocConfiguration.LogLevel"], true, out var logLevel)) SmocConfiguration.Defaults.LogLevel = logLevel;
    if (Enum.TryParse<StreamingService>(config["SmocConfiguration.ActiveService"], true, out var service)) SmocConfiguration.Defaults.ActiveService = service;
    if (long.TryParse(config["SmocConfiguration.SongCacheSizeBytes"], out var songCacheSize)) SmocConfiguration.Defaults.SongCacheSizeBytes = songCacheSize;
    if (int.TryParse(config["SmocConfiguration.SongCacheMaxElements"], out var songCacheMax)) SmocConfiguration.Defaults.SongCacheMaxElements = songCacheMax;
    if (long.TryParse(config["SmocConfiguration.AlbumCoverCacheSizeBytes"], out var artCacheSize)) SmocConfiguration.Defaults.AlbumCoverCacheSizeBytes = artCacheSize;
    if (int.TryParse(config["SmocConfiguration.AlbumCoverCacheMaxElements"], out var artCacheMax)) SmocConfiguration.Defaults.AlbumCoverCacheMaxElements = artCacheMax;
    if (int.TryParse(config["SmocConfiguration.VisualizerFps"], out var fps)) SmocConfiguration.Defaults.VisualizerFps = fps;
    if (bool.TryParse(config["SmocConfiguration.EnableLoudnessNormalization"], out var norm)) SmocConfiguration.Defaults.EnableLoudnessNormalization = norm;
    if (Enum.TryParse<LoudnessNormalizationMode>(config["SmocConfiguration.LoudnessNormalizationMode"], true, out var normMode)) SmocConfiguration.Defaults.LoudnessNormalizationMode = normMode;

    if (bool.TryParse(config["ListenHistoryConfig.Enabled"], out var enabled)) ListenHistoryConfig.Defaults.Enabled = enabled;
    if (int.TryParse(config["ListenHistoryConfig.MinimumPositionSeconds"], out var minSec)) ListenHistoryConfig.Defaults.MinimumPositionSeconds = minSec;
    if (double.TryParse(config["ListenHistoryConfig.MinimumFraction"], CultureInfo.InvariantCulture, out var minFrac)) ListenHistoryConfig.Defaults.MinimumFraction = minFrac;

    if (config["SoundCloudConfig.ClientId"] is { } clientId) SoundCloudConfig.Defaults.ClientId = clientId;
    if (config["SoundCloudConfig.AuthToken"] is { } authToken) SoundCloudConfig.Defaults.AuthToken = authToken;

    if (config["SubsonicConfig.ServerHost"] is { } flatHost) SubsonicConfig.Defaults.ServerHost = flatHost;
    if (int.TryParse(config["SubsonicConfig.ServerPort"], out var flatPort)) SubsonicConfig.Defaults.ServerPort = flatPort;
    if (config["SubsonicConfig.ServerScheme"] is { } flatScheme) SubsonicConfig.Defaults.ServerScheme = flatScheme;
    if (config["SubsonicConfig.Username"] is { } flatUser) SubsonicConfig.Defaults.Username = flatUser;
    if (config["SubsonicConfig.Password"] is { } flatPass) SubsonicConfig.Defaults.Password = flatPass;
    if (bool.TryParse(config["SubsonicConfig.UseToken"], out var flatToken)) SubsonicConfig.Defaults.UseToken = flatToken;

    if (config["YouTubeMusicConfig.PlayerId"] is { } flatPlayerId) YouTubeMusicConfig.Defaults.PlayerId = flatPlayerId;
  }

  /// <summary>
  /// Creates a streaming client based on the configured active service.
  /// </summary>
  /// <returns>An initialized <see cref="IStreamingClient"/>.</returns>
  private static IStreamingClient CreateStreamingClient() {
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
        throw new InvalidOperationException($"Unknown streaming service: {SmocConfiguration.Defaults.ActiveService}");
    }
  }
}
