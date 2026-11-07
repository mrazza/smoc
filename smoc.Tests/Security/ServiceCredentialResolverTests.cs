using System.Net;
using System.Text.Json;
using Smoc.Configuration;
using Smoc.Security;
using Smoc.Streaming.YouTubeMusic;
using Xunit;

namespace smoc.Tests.Security;

/// <summary>
/// Unit tests for <see cref="ServiceCredentialResolver"/>.
/// </summary>
public class ServiceCredentialResolverTests {
  #region Subsonic

  [Fact]
  public async Task ResolveSubsonic_ResolvesPasswordFromCredentialStore() {
    var originalHost = SubsonicConfig.Defaults.ServerHost;
    var originalUser = SubsonicConfig.Defaults.Username;
    var originalPass = SubsonicConfig.Defaults.Password;
    try {
      SubsonicConfig.Defaults.ServerHost = "subsonic.example.com";
      SubsonicConfig.Defaults.Username = "subsonicUser";
      SubsonicConfig.Defaults.Password = null;

      var store = new InMemoryCredentialStore();
      await store.SetCredentialAsync(CredentialTargets.Subsonic, "subsonicUser", "keyringPassword", TestContext.Current.CancellationToken);

      var credentials = await ServiceCredentialResolver.ResolveSubsonicAsync(store, TestContext.Current.CancellationToken);

      Assert.NotNull(credentials);
      Assert.Equal("subsonic.example.com", credentials.Host);
      Assert.Equal("subsonicUser", credentials.Username);
      Assert.Equal("keyringPassword", credentials.Password);
    } finally {
      SubsonicConfig.Defaults.ServerHost = originalHost;
      SubsonicConfig.Defaults.Username = originalUser;
      SubsonicConfig.Defaults.Password = originalPass;
    }
  }

  [Fact]
  public async Task ResolveSubsonic_FallsBackToConfig_WhenCredentialStoreHasNoPassword() {
    var originalHost = SubsonicConfig.Defaults.ServerHost;
    var originalUser = SubsonicConfig.Defaults.Username;
    var originalPass = SubsonicConfig.Defaults.Password;
    try {
      SubsonicConfig.Defaults.ServerHost = "subsonic.example.com";
      SubsonicConfig.Defaults.Username = "subsonicUser";
      SubsonicConfig.Defaults.Password = "configPassword";

      var store = new InMemoryCredentialStore();

      var credentials = await ServiceCredentialResolver.ResolveSubsonicAsync(store, TestContext.Current.CancellationToken);

      Assert.NotNull(credentials);
      Assert.Equal("configPassword", credentials.Password);
    } finally {
      SubsonicConfig.Defaults.ServerHost = originalHost;
      SubsonicConfig.Defaults.Username = originalUser;
      SubsonicConfig.Defaults.Password = originalPass;
    }
  }

  [Fact]
  public async Task ResolveSubsonic_ThrowsIfPasswordNotConfiguredAnywhere() {
    var originalHost = SubsonicConfig.Defaults.ServerHost;
    var originalUser = SubsonicConfig.Defaults.Username;
    var originalPass = SubsonicConfig.Defaults.Password;
    try {
      SubsonicConfig.Defaults.ServerHost = "subsonic.example.com";
      SubsonicConfig.Defaults.Username = "subsonicUser";
      SubsonicConfig.Defaults.Password = null;

      var store = new InMemoryCredentialStore();

      var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        ServiceCredentialResolver.ResolveSubsonicAsync(store, TestContext.Current.CancellationToken));
      Assert.Contains("Subsonic Password not configured", ex.Message);
    } finally {
      SubsonicConfig.Defaults.ServerHost = originalHost;
      SubsonicConfig.Defaults.Username = originalUser;
      SubsonicConfig.Defaults.Password = originalPass;
    }
  }

  [Fact]
  public async Task ResolveSubsonic_ThrowsIfHostNotConfigured() {
    var originalHost = SubsonicConfig.Defaults.ServerHost;
    var originalUser = SubsonicConfig.Defaults.Username;
    var originalPass = SubsonicConfig.Defaults.Password;
    try {
      SubsonicConfig.Defaults.ServerHost = null;
      SubsonicConfig.Defaults.Username = "subsonicUser";
      SubsonicConfig.Defaults.Password = "pass";

      var store = new InMemoryCredentialStore();

      var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        ServiceCredentialResolver.ResolveSubsonicAsync(store, TestContext.Current.CancellationToken));
      Assert.Contains("Subsonic Server Host not configured", ex.Message);
    } finally {
      SubsonicConfig.Defaults.ServerHost = originalHost;
      SubsonicConfig.Defaults.Username = originalUser;
      SubsonicConfig.Defaults.Password = originalPass;
    }
  }

  [Fact]
  public async Task ResolveSubsonic_ThrowsIfUsernameNotConfigured() {
    var originalHost = SubsonicConfig.Defaults.ServerHost;
    var originalUser = SubsonicConfig.Defaults.Username;
    var originalPass = SubsonicConfig.Defaults.Password;
    try {
      SubsonicConfig.Defaults.ServerHost = "subsonic.example.com";
      SubsonicConfig.Defaults.Username = null;
      SubsonicConfig.Defaults.Password = "pass";

      var store = new InMemoryCredentialStore();

      var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        ServiceCredentialResolver.ResolveSubsonicAsync(store, TestContext.Current.CancellationToken));
      Assert.Contains("Subsonic Username not configured", ex.Message);
    } finally {
      SubsonicConfig.Defaults.ServerHost = originalHost;
      SubsonicConfig.Defaults.Username = originalUser;
      SubsonicConfig.Defaults.Password = originalPass;
    }
  }

  #endregion

  #region SoundCloud

  [Fact]
  public async Task ResolveSoundCloud_ResolvesFromCredentialStore() {
    var originalToken = SoundCloudConfig.Defaults.AuthToken;
    var originalClient = SoundCloudConfig.Defaults.ClientId;
    try {
      SoundCloudConfig.Defaults.AuthToken = null;
      SoundCloudConfig.Defaults.ClientId = null;

      var store = new InMemoryCredentialStore();
      await store.SetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, "store-token-123", TestContext.Current.CancellationToken);
      await store.SetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountClientId, "store-client-456", TestContext.Current.CancellationToken);

      var credentials = await ServiceCredentialResolver.ResolveSoundCloudAsync(store, TestContext.Current.CancellationToken);

      Assert.NotNull(credentials);
      Assert.Equal("store-token-123", credentials.AuthToken);
      Assert.Equal("store-client-456", credentials.ClientId);
    } finally {
      SoundCloudConfig.Defaults.AuthToken = originalToken;
      SoundCloudConfig.Defaults.ClientId = originalClient;
    }
  }

  [Fact]
  public async Task ResolveSoundCloud_FallsBackToConfig_WhenNotInStore() {
    var originalToken = SoundCloudConfig.Defaults.AuthToken;
    var originalClient = SoundCloudConfig.Defaults.ClientId;
    try {
      SoundCloudConfig.Defaults.AuthToken = "config-token";
      SoundCloudConfig.Defaults.ClientId = "config-client";

      var store = new InMemoryCredentialStore();

      var credentials = await ServiceCredentialResolver.ResolveSoundCloudAsync(store, TestContext.Current.CancellationToken);

      Assert.NotNull(credentials);
      Assert.Equal("config-token", credentials.AuthToken);
      Assert.Equal("config-client", credentials.ClientId);
    } finally {
      SoundCloudConfig.Defaults.AuthToken = originalToken;
      SoundCloudConfig.Defaults.ClientId = originalClient;
    }
  }

  [Fact]
  public async Task ResolveSoundCloud_ReturnsNulls_WhenNoCredentialsConfigured() {
    var originalToken = SoundCloudConfig.Defaults.AuthToken;
    var originalClient = SoundCloudConfig.Defaults.ClientId;
    try {
      SoundCloudConfig.Defaults.AuthToken = null;
      SoundCloudConfig.Defaults.ClientId = null;

      var store = new InMemoryCredentialStore();

      var credentials = await ServiceCredentialResolver.ResolveSoundCloudAsync(store, TestContext.Current.CancellationToken);

      Assert.NotNull(credentials);
      Assert.Null(credentials.AuthToken);
      Assert.Null(credentials.ClientId);
    } finally {
      SoundCloudConfig.Defaults.AuthToken = originalToken;
      SoundCloudConfig.Defaults.ClientId = originalClient;
    }
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
