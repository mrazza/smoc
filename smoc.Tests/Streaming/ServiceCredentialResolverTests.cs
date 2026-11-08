using System.Net;
using System.Text.Json;
using Moq;
using Smoc.Configuration;
using Smoc.Security;
using Smoc.Streaming;
using Smoc.Streaming.YouTubeMusic;
using Xunit;

namespace smoc.Tests.Streaming;

/// <summary>
/// Unit tests for <see cref="ServiceCredentialResolver"/>.
/// </summary>
public class ServiceCredentialResolverTests {
  #region Subsonic

  [Fact]
  public async Task ResolveSubsonic_ResolvesPasswordFromCredentialStore() {
    var config = new SubsonicConfig {
      ServerHost = "subsonic.example.com",
      Username = "subsonicUser",
      Password = null
    };

    var store = new InMemoryCredentialStore();
    await store.SetCredentialAsync(CredentialTargets.Subsonic, "subsonicUser", "keyringPassword", TestContext.Current.CancellationToken);

    var credentials = await ServiceCredentialResolver.ResolveSubsonicAsync(store, config, TestContext.Current.CancellationToken);

    Assert.NotNull(credentials);
    Assert.Equal("subsonic.example.com", credentials.Host);
    Assert.Equal("subsonicUser", credentials.Username);
    Assert.Equal("keyringPassword", credentials.Password);
  }

  [Fact]
  public async Task ResolveSubsonic_FallsBackToConfig_WhenCredentialStoreHasNoPassword() {
    var config = new SubsonicConfig {
      ServerHost = "subsonic.example.com",
      Username = "subsonicUser",
      Password = "configPassword"
    };

    var store = new InMemoryCredentialStore();

    var credentials = await ServiceCredentialResolver.ResolveSubsonicAsync(store, config, TestContext.Current.CancellationToken);

    Assert.NotNull(credentials);
    Assert.Equal("configPassword", credentials.Password);
  }

  [Fact]
  public async Task ResolveSubsonic_ThrowsIfPasswordNotConfiguredAnywhere() {
    var config = new SubsonicConfig {
      ServerHost = "subsonic.example.com",
      Username = "subsonicUser",
      Password = null
    };

    var store = new InMemoryCredentialStore();

    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
      ServiceCredentialResolver.ResolveSubsonicAsync(store, config, TestContext.Current.CancellationToken));
    Assert.Contains("Subsonic Password not configured", ex.Message);
  }

  [Fact]
  public async Task ResolveSubsonic_SurfacesStoreException_WhenKeyringFailsAndNoFallbackConfigured() {
    var config = new SubsonicConfig {
      ServerHost = "subsonic.example.com",
      Username = "subsonicUser",
      Password = null
    };

    var failingStore = new Mock<ICredentialStore>();
    var inner = new InvalidOperationException("Keyring collection is locked");
    failingStore.Setup(s => s.GetCredentialAsync(CredentialTargets.Subsonic, "subsonicUser", It.IsAny<CancellationToken>()))
                .ThrowsAsync(inner);

    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
      ServiceCredentialResolver.ResolveSubsonicAsync(failingStore.Object, config, TestContext.Current.CancellationToken));
    Assert.Contains("Keyring collection is locked", ex.Message);
    Assert.Same(inner, ex.InnerException);
  }

  [Fact]
  public async Task ResolveSubsonic_ThrowsIfHostNotConfigured() {
    var config = new SubsonicConfig {
      ServerHost = null,
      Username = "subsonicUser",
      Password = "pass"
    };

    var store = new InMemoryCredentialStore();

    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
      ServiceCredentialResolver.ResolveSubsonicAsync(store, config, TestContext.Current.CancellationToken));
    Assert.Contains("Subsonic Server Host not configured", ex.Message);
  }

  [Fact]
  public async Task ResolveSubsonic_ThrowsIfUsernameNotConfigured() {
    var config = new SubsonicConfig {
      ServerHost = "subsonic.example.com",
      Username = null,
      Password = "pass"
    };

    var store = new InMemoryCredentialStore();

    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
      ServiceCredentialResolver.ResolveSubsonicAsync(store, config, TestContext.Current.CancellationToken));
    Assert.Contains("Subsonic Username not configured", ex.Message);
  }

  #endregion

  #region SoundCloud

  [Fact]
  public async Task ResolveSoundCloud_ResolvesAuthTokenFromStoreAndClientIdFromConfig() {
    var config = new SoundCloudConfig {
      AuthToken = null,
      ClientId = "config-client-456"
    };

    var store = new InMemoryCredentialStore();
    await store.SetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, "store-token-123", TestContext.Current.CancellationToken);

    var credentials = await ServiceCredentialResolver.ResolveSoundCloudAsync(store, config, TestContext.Current.CancellationToken);

    Assert.NotNull(credentials);
    Assert.Equal("store-token-123", credentials.AuthToken);
    Assert.Equal("config-client-456", credentials.ClientId);
  }

  [Fact]
  public async Task ResolveSoundCloud_FallsBackToConfig_WhenNotInStore() {
    var config = new SoundCloudConfig {
      AuthToken = "config-token",
      ClientId = "config-client"
    };

    var store = new InMemoryCredentialStore();

    var credentials = await ServiceCredentialResolver.ResolveSoundCloudAsync(store, config, TestContext.Current.CancellationToken);

    Assert.NotNull(credentials);
    Assert.Equal("config-token", credentials.AuthToken);
    Assert.Equal("config-client", credentials.ClientId);
  }

  [Fact]
  public async Task ResolveSoundCloud_ReturnsNulls_WhenNoCredentialsConfigured() {
    var config = new SoundCloudConfig {
      AuthToken = null,
      ClientId = null
    };

    var store = new InMemoryCredentialStore();

    var credentials = await ServiceCredentialResolver.ResolveSoundCloudAsync(store, config, TestContext.Current.CancellationToken);

    Assert.NotNull(credentials);
    Assert.Null(credentials.AuthToken);
    Assert.Null(credentials.ClientId);
  }

  #endregion

  #region YouTube Music

  [Fact]
  public async Task ResolveYouTubeMusic_ResolvesCookiesAndTokensFromCredentialStore() {
    string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    string dummyCookiesPath = Path.Combine(tempDir, "cookies.txt");
    string dummyTokensPath = Path.Combine(tempDir, "tokens.json");

    try {
      var store = new InMemoryCredentialStore();
      await store.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, "SID=abc123; HSID=def456;", TestContext.Current.CancellationToken);
      var tokens = new YtmTokens("poVal", "rolloutVal", "visitorVal");
      await store.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, JsonSerializer.Serialize(tokens), TestContext.Current.CancellationToken);

      var session = await ServiceCredentialResolver.ResolveYouTubeMusicAsync(
        store,
        cookiesPath: dummyCookiesPath,
        tokensPath: dummyTokensPath,
        cancellationToken: TestContext.Current.CancellationToken
      );

      Assert.NotNull(session);
      Assert.Equal(2, session.Cookies.Count);
      Assert.Equal("poVal", session.Tokens.PoToken);
    } finally {
      Directory.Delete(tempDir, true);
    }
  }

  [Fact]
  public async Task ResolveYouTubeMusic_GeneratesTokensAndSavesToStore_WhenTokensMissingInStore() {
    string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    string dummyCookiesPath = Path.Combine(tempDir, "cookies.txt");
    string dummyTokensPath = Path.Combine(tempDir, "tokens.json");

    try {
      var store = new InMemoryCredentialStore();
      await store.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, "SID=abc123; HSID=def456;", TestContext.Current.CancellationToken);

      bool tokenGeneratorCalled = false;
      Task<YtmTokens> MockTokenGenerator(List<Cookie> cookies) {
        tokenGeneratorCalled = true;
        return Task.FromResult(new YtmTokens("generatedPo", "generatedRollout", "generatedVisitor"));
      }

      var session = await ServiceCredentialResolver.ResolveYouTubeMusicAsync(
        store,
        cookiesPath: dummyCookiesPath,
        tokensPath: dummyTokensPath,
        tokenGenerator: MockTokenGenerator,
        cancellationToken: TestContext.Current.CancellationToken
      );

      Assert.NotNull(session);
      Assert.True(tokenGeneratorCalled);
      Assert.Equal("generatedPo", session.Tokens.PoToken);

      // Verify tokens were saved to store
      string? savedTokensJson = await store.GetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, TestContext.Current.CancellationToken);
      Assert.NotNull(savedTokensJson);
      var savedTokens = JsonSerializer.Deserialize<YtmTokens>(savedTokensJson);
      Assert.Equal("generatedPo", savedTokens?.PoToken);

      // Verify plaintext tokens.json was NOT written
      Assert.False(File.Exists(dummyTokensPath));
    } finally {
      Directory.Delete(tempDir, true);
    }
  }

  [Fact]
  public async Task ResolveYouTubeMusic_FallsBackToLegacyCookiesAndTokensFiles_WhenNotInStore() {
    string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    string cookiesFile = Path.Combine(tempDir, "cookies.txt");
    string tokensFile = Path.Combine(tempDir, "tokens.json");

    try {
      await File.WriteAllTextAsync(cookiesFile, "SID=fileCookie123; HSID=fileCookie456;", TestContext.Current.CancellationToken);
      var tokens = new YtmTokens("filePo", "fileRollout", "fileVisitor");
      await File.WriteAllTextAsync(tokensFile, JsonSerializer.Serialize(tokens), TestContext.Current.CancellationToken);

      var store = new InMemoryCredentialStore();
      var session = await ServiceCredentialResolver.ResolveYouTubeMusicAsync(
        store,
        cookiesPath: cookiesFile,
        tokensPath: tokensFile,
        cancellationToken: TestContext.Current.CancellationToken
      );

      Assert.NotNull(session);
      Assert.Equal(2, session.Cookies.Count);
      Assert.Equal("filePo", session.Tokens.PoToken);
    } finally {
      Directory.Delete(tempDir, true);
    }
  }

  [Fact]
  public async Task ResolveYouTubeMusic_GeneratesTokensAndSavesToLegacyFile_WhenTokensMissingInLegacyFiles() {
    string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    string cookiesFile = Path.Combine(tempDir, "cookies.txt");
    string tokensFile = Path.Combine(tempDir, "tokens.json");

    try {
      await File.WriteAllTextAsync(cookiesFile, "SID=fileCookie123; HSID=fileCookie456;", TestContext.Current.CancellationToken);

      bool tokenGeneratorCalled = false;
      Task<YtmTokens> MockTokenGenerator(List<Cookie> cookies) {
        tokenGeneratorCalled = true;
        return Task.FromResult(new YtmTokens("genLegacyPo", "genLegacyRollout", "genLegacyVisitor"));
      }

      var store = new InMemoryCredentialStore();
      var session = await ServiceCredentialResolver.ResolveYouTubeMusicAsync(
        store,
        cookiesPath: cookiesFile,
        tokensPath: tokensFile,
        tokenGenerator: MockTokenGenerator,
        cancellationToken: TestContext.Current.CancellationToken
      );

      Assert.NotNull(session);
      Assert.True(tokenGeneratorCalled);
      Assert.Equal("genLegacyPo", session.Tokens.PoToken);

      // Verify tokens were saved to the legacy file
      Assert.True(File.Exists(tokensFile));
      string savedContent = await File.ReadAllTextAsync(tokensFile, TestContext.Current.CancellationToken);
      var savedTokens = JsonSerializer.Deserialize<YtmTokens>(savedContent);
      Assert.Equal("genLegacyPo", savedTokens?.PoToken);
    } finally {
      Directory.Delete(tempDir, true);
    }
  }

  [Fact]
  public async Task ResolveYouTubeMusic_ReturnsNull_WhenNoStoreOrFileCredentials() {
    string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    string nonexistentCookies = Path.Combine(tempDir, "cookies.txt");
    string nonexistentTokens = Path.Combine(tempDir, "tokens.json");

    var store = new InMemoryCredentialStore();
    var session = await ServiceCredentialResolver.ResolveYouTubeMusicAsync(
      store,
      cookiesPath: nonexistentCookies,
      tokensPath: nonexistentTokens,
      cancellationToken: TestContext.Current.CancellationToken
    );

    Assert.Null(session);
  }

  #endregion
}
