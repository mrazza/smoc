using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Moq;
using Smoc.Cli.Auth;
using Smoc.Cli.Auth.YouTubeMusic;
using Smoc.Security;
using Smoc.Streaming.YouTubeMusic;
using Xunit;

namespace smoc.Tests.Cli;

public class AuthServiceTests : IDisposable {
  private readonly string _tempDir;
  private readonly string _configPath;
  private readonly string _cookiesPath;
  private readonly string _tokensPath;
  private readonly InMemoryCredentialStore _store;

  public AuthServiceTests() {
    _tempDir = Path.Combine(Path.GetTempPath(), "smoc_auth_tests_" + Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(_tempDir);
    _configPath = Path.Combine(_tempDir, "config.json");
    _cookiesPath = Path.Combine(_tempDir, "cookies.txt");
    _tokensPath = Path.Combine(_tempDir, "tokens.json");
    _store = new InMemoryCredentialStore();
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

  private AuthService CreateService(ICredentialStore? store = null, Func<List<Cookie>, Task<YtmTokens>>? tokenGenerator = null) {
    tokenGenerator ??= _ => Task.FromResult(new YtmTokens("mockPoToken", "mockRollout", "mockVisitor"));
    return new AuthService(store ?? _store, _configPath, _cookiesPath, _tokensPath, tokenGenerator);
  }

  [Fact]
  public async Task LoginSubsonicAsync_SavesCredentialsAndSanitizesConfigWithoutLeavingPlaintextBackup() {
    var initialConfig = new JsonObject {
      ["SubsonicConfig"] = new JsonObject {
        ["Password"] = "oldPlaintextSecret"
      }
    };
    await File.WriteAllTextAsync(_configPath, initialConfig.ToJsonString(), TestContext.Current.CancellationToken);

    using var service = CreateService();

    await service.LoginSubsonicAsync("subsonic.example.com", 443, "https", "razza", "secretpass", useToken: true, verifyConnection: false, TestContext.Current.CancellationToken);

    string? storedPass = await _store.GetCredentialAsync(CredentialTargets.Subsonic, "razza", TestContext.Current.CancellationToken);
    Assert.Equal("secretpass", storedPass);

    string configJson = await File.ReadAllTextAsync(_configPath, TestContext.Current.CancellationToken);
    var node = JsonNode.Parse(configJson);
    Assert.Equal("subsonic.example.com", node?["SubsonicConfig"]?["ServerHost"]?.GetValue<string>());
    Assert.Equal(443, node?["SubsonicConfig"]?["ServerPort"]?.GetValue<int>());
    Assert.Equal("razza", node?["SubsonicConfig"]?["Username"]?.GetValue<string>());
    Assert.Null(node?["SubsonicConfig"]?["Password"]);
    Assert.False(File.Exists(_configPath + ".bak"));
  }

  [Fact]
  public async Task LoginSubsonicAsync_ThrowsOnMissingArguments() {
    using var service = CreateService();

    await Assert.ThrowsAsync<ArgumentException>(() => service.LoginSubsonicAsync("", 443, "https", "user", "pass", true, false, TestContext.Current.CancellationToken));
    await Assert.ThrowsAsync<ArgumentException>(() => service.LoginSubsonicAsync("host", 443, "https", "", "pass", true, false, TestContext.Current.CancellationToken));
    await Assert.ThrowsAsync<ArgumentException>(() => service.LoginSubsonicAsync("host", 443, "https", "user", "", true, false, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task LoginSoundCloudAsync_SavesTokenToStoreAndClientIdToConfigOnly() {
    using var service = CreateService();

    await service.LoginSoundCloudAsync("my-client-id", "my-auth-token", TestContext.Current.CancellationToken);

    string? storedToken = await _store.GetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, TestContext.Current.CancellationToken);
    string? storedClientId = await _store.GetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountClientId, TestContext.Current.CancellationToken);
    Assert.Equal("my-auth-token", storedToken);
    Assert.Null(storedClientId);

    string configJson = await File.ReadAllTextAsync(_configPath, TestContext.Current.CancellationToken);
    var node = JsonNode.Parse(configJson);
    Assert.Equal("my-client-id", node?["SoundCloudConfig"]?["ClientId"]?.GetValue<string>());
    Assert.Null(node?["SoundCloudConfig"]?["AuthToken"]);
    Assert.False(File.Exists(_configPath + ".bak"));
  }

  [Fact]
  public async Task ImportCookiesAsync_FileNotFound_ReturnsFailure() {
    using var service = CreateService();

    var result = await service.ImportCookiesAsync(Path.Combine(_tempDir, "nonexistent.txt"), deleteSource: false, TestContext.Current.CancellationToken);

    Assert.False(result.Success);
    Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task ImportCookiesAsync_Success_StoresCookiesAndTokens_DeletesSourceIfRequested() {
    using var service = CreateService();
    string testCookies = "SID=abcd1234; HSID=xyz9876;";
    await File.WriteAllTextAsync(_cookiesPath, testCookies, TestContext.Current.CancellationToken);

    var result = await service.ImportCookiesAsync(_cookiesPath, deleteSource: true, TestContext.Current.CancellationToken);

    Assert.True(result.Success);
    Assert.Equal(2, result.CookieCount);
    Assert.True(result.TokensGenerated);
    Assert.True(result.SourceDeleted);
    Assert.False(File.Exists(_cookiesPath));

    string? storedCookies = await _store.GetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, TestContext.Current.CancellationToken);
    Assert.Equal(testCookies, storedCookies);

    string? storedTokens = await _store.GetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, TestContext.Current.CancellationToken);
    Assert.NotNull(storedTokens);
    var parsedTokens = JsonSerializer.Deserialize<YtmTokens>(storedTokens);
    Assert.Equal("mockPoToken", parsedTokens?.PoToken);
  }

  [Fact]
  public async Task ImportCookiesAsync_DefaultsToConfiguredCookiesPath_WhenPathOmittedOrWhitespace() {
    using var service = CreateService();
    string testCookies = "SID=defaultpath123; HSID=defaultpath456;";
    await File.WriteAllTextAsync(_cookiesPath, testCookies, TestContext.Current.CancellationToken);

    var result = await service.ImportCookiesAsync(null, deleteSource: false, TestContext.Current.CancellationToken);

    Assert.True(result.Success);
    Assert.Equal(2, result.CookieCount);
    string? storedCookies = await _store.GetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, TestContext.Current.CancellationToken);
    Assert.Equal(testCookies, storedCookies);
  }

  [Fact]
  public async Task GenerateTokensAsync_RegeneratesTokensFromStoredCookies() {
    using var service = CreateService(tokenGenerator: _ => Task.FromResult(new YtmTokens("newPoToken", "newRollout", "newVisitor")));
    await _store.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, "SID=stored123; HSID=stored456;", TestContext.Current.CancellationToken);
    await _store.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, JsonSerializer.Serialize(new YtmTokens("oldPo", "oldRollout", "oldVisitor")), TestContext.Current.CancellationToken);

    await service.GenerateTokensAsync(TestContext.Current.CancellationToken);

    string? storedTokens = await _store.GetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, TestContext.Current.CancellationToken);
    Assert.NotNull(storedTokens);
    var parsedTokens = JsonSerializer.Deserialize<YtmTokens>(storedTokens);
    Assert.Equal("newPoToken", parsedTokens?.PoToken);
  }

  [Fact]
  public async Task GenerateTokensAsync_FallsBackToLegacyCookiesFile_WhenNotInStore() {
    using var service = CreateService(tokenGenerator: _ => Task.FromResult(new YtmTokens("legacyPoToken", "legacyRollout", "legacyVisitor")));
    await File.WriteAllTextAsync(_cookiesPath, "SID=legacy123; HSID=legacy456;", TestContext.Current.CancellationToken);

    await service.GenerateTokensAsync(TestContext.Current.CancellationToken);

    Assert.True(File.Exists(_tokensPath));
    string savedTokens = await File.ReadAllTextAsync(_tokensPath, TestContext.Current.CancellationToken);
    var parsedTokens = JsonSerializer.Deserialize<YtmTokens>(savedTokens);
    Assert.Equal("legacyPoToken", parsedTokens?.PoToken);
  }

  [Fact]
  public async Task GenerateTokensAsync_ThrowsWhenNoCookiesExist() {
    using var service = CreateService();

    await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateTokensAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task GetStatusAsync_ReturnsStatusAcrossServices() {
    using var service = CreateService();
    await _store.SetCredentialAsync(CredentialTargets.Subsonic, "razza", "secretpass", TestContext.Current.CancellationToken);
    await _store.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, "cookie=val;", TestContext.Current.CancellationToken);
    await _store.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, "{}", TestContext.Current.CancellationToken);

    var initialConfig = new JsonObject {
      ["SubsonicConfig"] = new JsonObject {
        ["ServerHost"] = "subsonic.local",
        ["Username"] = "razza"
      }
    };
    await File.WriteAllTextAsync(_configPath, initialConfig.ToJsonString(), TestContext.Current.CancellationToken);

    var statuses = await service.GetStatusAsync(TestContext.Current.CancellationToken);

    var subsonic = Assert.Single(statuses, s => s.Service == "Subsonic");
    Assert.Equal(AuthStatusState.Authenticated, subsonic.Status);
    Assert.Equal(CredentialStorageKind.Keyring, subsonic.Storage);
    Assert.Contains("razza @ subsonic.local", subsonic.Details);

    var ytm = Assert.Single(statuses, s => s.Service == "YouTube Music");
    Assert.Equal(AuthStatusState.Authenticated, ytm.Status);
    Assert.Equal(CredentialStorageKind.Keyring, ytm.Storage);

    var soundcloud = Assert.Single(statuses, s => s.Service == "SoundCloud");
    Assert.Equal(AuthStatusState.NotConfigured, soundcloud.Status);
  }

  [Fact]
  public async Task GetStatusAsync_SkipsUnavailableStoreInComposite_ReportsNotConfiguredInsteadOfError() {
    var unavailableKeyring = new Mock<ICredentialStore>();
    unavailableKeyring.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
    unavailableKeyring.Setup(s => s.GetCredentialAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                      .ThrowsAsync(new InvalidOperationException("No D-Bus session bus"));

    var envStore = new EnvironmentCredentialStore(_ => null);
    var composite = new CompositeCredentialStore(unavailableKeyring.Object, envStore);

    using var service = CreateService(composite);
    var statuses = await service.GetStatusAsync(TestContext.Current.CancellationToken);

    Assert.All(statuses, s => Assert.Equal(AuthStatusState.NotConfigured, s.Status));
  }

  [Fact]
  public async Task LogoutAsync_RemovesCredentials() {
    using var service = CreateService();
    await _store.SetCredentialAsync(CredentialTargets.Subsonic, "razza", "pass", TestContext.Current.CancellationToken);
    await _store.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, "cookie", TestContext.Current.CancellationToken);
    await _store.SetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, "token", TestContext.Current.CancellationToken);

    var config = new JsonObject {
      ["SubsonicConfig"] = new JsonObject {
        ["Username"] = "razza"
      }
    };
    await File.WriteAllTextAsync(_configPath, config.ToJsonString(), TestContext.Current.CancellationToken);

    bool loggedOutYtm = await service.LogoutAsync("youtubemusic", TestContext.Current.CancellationToken);
    Assert.True(loggedOutYtm);
    Assert.Null(await _store.GetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, TestContext.Current.CancellationToken));

    bool loggedOutAll = await service.LogoutAsync("all", TestContext.Current.CancellationToken);
    Assert.True(loggedOutAll);
    Assert.Null(await _store.GetCredentialAsync(CredentialTargets.Subsonic, "razza", TestContext.Current.CancellationToken));
    Assert.Null(await _store.GetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task LogoutAsync_ReturnsFalseWhenCredentialIsOnlyInReadOnlyEnvironmentStore() {
    var envStore = new EnvironmentCredentialStore(key => key == "SMOC_SOUNDCLOUD_AUTH_TOKEN" ? "env_token" : null);
    var composite = new CompositeCredentialStore(_store, envStore);

    using var service = CreateService(composite);
    bool removed = await service.LogoutAsync("soundcloud", TestContext.Current.CancellationToken);

    Assert.False(removed);
  }

  [Fact]
  public async Task LogoutAsync_ThrowsOnUnknownService() {
    using var service = CreateService();
    await Assert.ThrowsAsync<ArgumentException>(() => service.LogoutAsync("unknown_svc", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task MigrateAsync_MigratesPlaintextSecretsAndBacksUpFiles() {
    using var service = CreateService();

    var config = new JsonObject {
      ["SubsonicConfig"] = new JsonObject {
        ["ServerHost"] = "sub.local",
        ["Username"] = "matt",
        ["Password"] = "plaintextSubsonicPass"
      },
      ["SoundCloudConfig"] = new JsonObject {
        ["ClientId"] = "sc-client",
        ["AuthToken"] = "plaintextScToken"
      }
    };
    await File.WriteAllTextAsync(_configPath, config.ToJsonString(), TestContext.Current.CancellationToken);
    await File.WriteAllTextAsync(_cookiesPath, "COOKIE_A=123; COOKIE_B=456;", TestContext.Current.CancellationToken);
    await File.WriteAllTextAsync(_tokensPath, JsonSerializer.Serialize(new YtmTokens("po1", "ro1", "vis1")), TestContext.Current.CancellationToken);

    var result = await service.MigrateAsync(backupPlaintextFiles: true, TestContext.Current.CancellationToken);

    Assert.True(result.Success);
    Assert.Contains(result.MigratedItems, i => i.Contains("Subsonic password"));
    Assert.Contains(result.MigratedItems, i => i.Contains("SoundCloud"));
    Assert.Contains(result.MigratedItems, i => i.Contains("YouTube Music"));

    Assert.Equal("plaintextSubsonicPass", await _store.GetCredentialAsync(CredentialTargets.Subsonic, "matt", TestContext.Current.CancellationToken));
    Assert.Equal("plaintextScToken", await _store.GetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, TestContext.Current.CancellationToken));
    Assert.NotNull(await _store.GetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, TestContext.Current.CancellationToken));

    // Verify config is sanitized
    var sanitizedConfig = JsonNode.Parse(await File.ReadAllTextAsync(_configPath, TestContext.Current.CancellationToken));
    Assert.Null(sanitizedConfig?["SubsonicConfig"]?["Password"]);
    Assert.Null(sanitizedConfig?["SoundCloudConfig"]?["AuthToken"]);

    // Verify backup files exist and originals are cleaned
    Assert.False(File.Exists(_cookiesPath));
    Assert.True(File.Exists(_cookiesPath + ".bak"));
    Assert.False(File.Exists(_tokensPath));
    Assert.True(File.Exists(_tokensPath + ".bak"));
  }
}
