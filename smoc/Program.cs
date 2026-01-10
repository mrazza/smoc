using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Smoc.Streaming;
using Smoc.Streaming.YouTubeMusic;
using Smoc.Ui;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;

namespace Smoc;

/// <summary>
/// Trivial entry point for the application that immediately hands off
/// initialization responsibility to the MainWindow.
/// </summary>
public static class Program
{
    private static readonly string CONFIG_PATH = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.config/smoc/config.json";
    private static readonly string COOKIES_PATH = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.config/smoc/cookies.txt";
    private static readonly string TOKENS_PATH = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.config/smoc/tokens.json";
    private static readonly string LOG_PATH = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.config/smoc/log_{Date}.txt";
    public static readonly string PRODUCT_NAME = "SMoC";

    /// <summary>
    /// The main entry point for the application.s
    /// </summary>
    public static async Task Main(string[] args)
    {
        RootCommand rootCommand = new RootCommand("Streaming Music over Console (SMoC)");

        Command generateTokensCommand = new Command("--gentokens", "Generate PO Token and Visitor Token.");
        rootCommand.Subcommands.Add(generateTokensCommand);
        generateTokensCommand.SetAction(async (_) =>
        {
            var cookies = YtmStreamingClient.GetCookiesFromFile(COOKIES_PATH);
            var tokens = await YtmStreamingClient.GenerateTokensAsync(cookies);
            File.WriteAllText(TOKENS_PATH, JsonSerializer.Serialize(tokens));
        });

        rootCommand.SetAction((_) =>
        {
            if (File.Exists(CONFIG_PATH))
            {
                var configFile = File.ReadAllText(CONFIG_PATH);
                ConfigurationManager.RuntimeConfig = configFile;
            }
            Console.WriteLine(ConfigurationManager.RuntimeConfig);
            ConfigurationManager.Enable(ConfigLocations.AppResources | ConfigLocations.Runtime);

            using IApplication application = Application.Create().Init();

            using ILoggerFactory factory = LoggerFactory.Create(
                builder => builder.SetMinimumLevel(LogLevel.Debug).AddFile(LOG_PATH, LogLevel.Debug, retainedFileCountLimit: 2));
            ILogger logger = factory.CreateLogger("SMoC");
            Logging.Logger = logger;
            logger.LogInformation("SMoC starting...");

            application.Mouse.IsMouseDisabled = true;
            IStreamingClient streamingClient = CreateStreamingClient();
            using var window = new MainWindow(streamingClient);
            application.Run(window);
        });

        await rootCommand.Parse(args).InvokeAsync();
    }

    private static YtmStreamingClient CreateStreamingClient()
    {
        if (!File.Exists(COOKIES_PATH) || !File.Exists(TOKENS_PATH))
        {
            Logging.Information("Cookies or tokens not found. Creating new YTM client without authentication.");
            return YtmStreamingClient.Create();
        }

        Logging.Information("Cookies and tokens found. Creating new YTM client with authentication.");
        var cookies = YtmStreamingClient.GetCookiesFromFile(COOKIES_PATH);
        var tokens = JsonSerializer.Deserialize<YtmTokens>(File.ReadAllText(TOKENS_PATH));
        return YtmStreamingClient.Create(cookies, tokens!);
    }
}
