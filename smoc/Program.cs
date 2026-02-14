using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Smoc.Configuration;
using Smoc.Services.Caching;
using Smoc.Streaming;
using Smoc.Streaming.YouTubeMusic;
using Smoc.Ui;
using Smoc.Ui.Drawing;
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

    Command generateTokensCommand = new Command("--gentokens", "Generate PO Token and Visitor Token.");
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

      using IApplication application = Application.Create().Init();

      using ILoggerFactory factory = LoggerFactory.Create(
          builder => builder.SetMinimumLevel(SmocConfiguration.LogLevel).AddFile(_logPath, SmocConfiguration.LogLevel, retainedFileCountLimit: 2));
      ILogger logger = factory.CreateLogger("SMoC");
      Logging.Logger = logger;
      logger.LogInformation("SMoC starting...");

      application.Mouse.IsMouseDisabled = true;
      VimKeyBindings.AddNavigationKeyBindings(application.Keyboard.KeyBindings);
      IStreamingClient streamingClient = CreateStreamingClient();
      var sixelDriver = new SixelDriver(application);
      sixelDriver.Initialize();
      using var window = new MainWindow(streamingClient, sixelDriver);
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
  /// Creates a streaming client, optionally using cookies and tokens if available.
  /// </summary>
  /// <returns>An initialized <see cref="YtmStreamingClient"/>.</returns>
  private static YtmStreamingClient CreateStreamingClient() {
    if (!File.Exists(_cookiesPath) || !File.Exists(_tokensPath)) {
      Logging.Information("Cookies or tokens not found. Creating new YTM client without authentication.");
      return YtmStreamingClient.Create(new TempFileCacheService("songs"), new TempFileCacheService("art"));
    }

    Logging.Information("Cookies and tokens found. Creating new YTM client with authentication.");
    var cookies = YtmStreamingClient.GetCookiesFromFile(_cookiesPath);
    var tokens = JsonSerializer.Deserialize<YtmTokens>(File.ReadAllText(_tokensPath));
    return YtmStreamingClient.Create(cookies, tokens!, new TempFileCacheService("songs"), new TempFileCacheService("art"));
  }
}
