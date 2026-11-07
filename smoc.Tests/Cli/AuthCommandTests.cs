using System.CommandLine;
using Smoc.Cli.Auth;
using Smoc.Security;
using Xunit;

namespace smoc.Tests.Cli;

public class AuthCommandTests : IDisposable {
  private readonly string _tempDir;
  private readonly string _configPath;
  private readonly string _cookiesPath;
  private readonly string _tokensPath;
  private readonly InMemoryCredentialStore _store;
  private readonly TestConsoleIO _console;
  private readonly AuthService _authService;
  private readonly Command _authCommand;

  public AuthCommandTests() {
    _tempDir = Path.Combine(Path.GetTempPath(), "smoc_cmd_tests_" + Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(_tempDir);
    _configPath = Path.Combine(_tempDir, "config.json");
    _cookiesPath = Path.Combine(_tempDir, "cookies.txt");
    _tokensPath = Path.Combine(_tempDir, "tokens.json");
    _store = new InMemoryCredentialStore();
    _console = new TestConsoleIO();
    _authService = new AuthService(_store, _configPath, _cookiesPath, _tokensPath, _ => Task.FromResult(new Smoc.Streaming.YouTubeMusic.YtmTokens("po", "ro", "vis")));
    _authCommand = AuthCommand.Create(_authService, _console);
  }

  public void Dispose() {
    try {
      if (Directory.Exists(_tempDir)) {
        Directory.Delete(_tempDir, recursive: true);
      }
    } catch {
      // Best-effort cleanup
    }
  }

  [Fact]
  public async Task AuthStatus_PrintsFormattedTable() {
    RootCommand root = new() { _authCommand };

    int exitCode = await root.Parse("auth status").InvokeAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(0, exitCode);
    Assert.Contains("SERVICE", _console.Output);
    Assert.Contains("STATUS", _console.Output);
    Assert.Contains("STORAGE", _console.Output);
    Assert.Contains("Subsonic", _console.Output);
    Assert.Contains("YouTube Music", _console.Output);
    Assert.Contains("SoundCloud", _console.Output);
  }

  [Fact]
  public async Task AuthLoginSubsonic_NonInteractiveOptions_Succeeds() {
    RootCommand root = new() { _authCommand };

    int exitCode = await root.Parse("auth login subsonic --host sub.example.com --port 443 --scheme https -u testuser --password secret123 --no-verify").InvokeAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(0, exitCode);
    Assert.Contains("Success! Saved server settings to config.json and password to OS Keyring.", _console.Output);
    Assert.Equal("secret123", await _store.GetCredentialAsync(CredentialTargets.Subsonic, "testuser", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task AuthLoginSubsonic_InteractivePrompt_Succeeds() {
    RootCommand root = new() { _authCommand };

    _console.QueueInput("interactive.server.com"); // host
    _console.QueueInput("443");                    // port
    _console.QueueInput("interuser");              // username
    _console.QueueInput("interpass");              // password (via ReadPassword)

    int exitCode = await root.Parse("auth login subsonic --no-verify").InvokeAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(0, exitCode);
    Assert.Contains("Success! Saved server settings to config.json and password to OS Keyring.", _console.Output);
    Assert.Equal("interpass", await _store.GetCredentialAsync(CredentialTargets.Subsonic, "interuser", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task AuthLoginSoundCloud_Succeeds() {
    RootCommand root = new() { _authCommand };

    int exitCode = await root.Parse("auth login soundcloud --client-id sc-id-123 --token sc-token-456").InvokeAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(0, exitCode);
    Assert.Contains("Success! Saved SoundCloud credentials to OS Keyring.", _console.Output);
    Assert.Equal("sc-token-456", await _store.GetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task AuthImportCookies_Succeeds() {
    await File.WriteAllTextAsync(_cookiesPath, "TEST_COOKIE=123;", TestContext.Current.CancellationToken);
    RootCommand root = new() { _authCommand };

    int exitCode = await root.Parse($"auth import-cookies \"{_cookiesPath}\" --delete-source").InvokeAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(0, exitCode);
    Assert.Contains("Success:", _console.Output);
    Assert.Contains("Plaintext cookies file deleted.", _console.Output);
    Assert.Equal("TEST_COOKIE=123;", await _store.GetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task AuthLogout_Succeeds() {
    await _store.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, "c=1;", TestContext.Current.CancellationToken);
    RootCommand root = new() { _authCommand };

    int exitCode = await root.Parse("auth logout youtubemusic").InvokeAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(0, exitCode);
    Assert.Contains("Logged out of youtubemusic.", _console.Output);
    Assert.Null(await _store.GetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task AuthMigrate_Succeeds() {
    await File.WriteAllTextAsync(_cookiesPath, "COOKIE=val;", TestContext.Current.CancellationToken);
    RootCommand root = new() { _authCommand };

    int exitCode = await root.Parse("auth migrate --no-backup").InvokeAsync(cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(0, exitCode);
    Assert.Contains("Successfully migrated credentials to OS Keyring:", _console.Output);
    Assert.False(File.Exists(_cookiesPath));
  }
}
