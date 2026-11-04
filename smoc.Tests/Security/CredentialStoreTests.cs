using System.Text;
using Moq;
using Smoc.Security;
using Xunit;

namespace smoc.Tests.Security;

/// <summary>
/// Comprehensive unit tests for cross-platform credential stores and factories.
/// </summary>
public class CredentialStoreTests {
  #region InMemoryCredentialStore Tests

  [Fact]
  public async Task InMemory_SetAndGetAsync_StoresAndRetrievesValue() {
    var store = new InMemoryCredentialStore();
    string service = CredentialTargets.Subsonic;
    string account = "testuser";
    string secret = "secret_password_123";

    await store.SetCredentialAsync(service, account, secret, TestContext.Current.CancellationToken);
    string? result = await store.GetCredentialAsync(service, account, TestContext.Current.CancellationToken);

    Assert.Equal(secret, result);
  }

  [Fact]
  public async Task InMemory_GetAsync_NonExistentKey_ReturnsNull() {
    var store = new InMemoryCredentialStore();
    string? result = await store.GetCredentialAsync("nonexistent", "nobody", TestContext.Current.CancellationToken);
    Assert.Null(result);
  }

  [Fact]
  public async Task InMemory_UpdateAsync_OverwritesExistingValue() {
    var store = new InMemoryCredentialStore();
    string service = CredentialTargets.SoundCloud;
    string account = CredentialTargets.AccountAuthToken;

    await store.SetCredentialAsync(service, account, "initial_token", TestContext.Current.CancellationToken);
    await store.SetCredentialAsync(service, account, "updated_token", TestContext.Current.CancellationToken);

    string? result = await store.GetCredentialAsync(service, account, TestContext.Current.CancellationToken);
    Assert.Equal("updated_token", result);
  }

  [Fact]
  public async Task InMemory_DeleteAsync_RemovesValue() {
    var store = new InMemoryCredentialStore();
    string service = CredentialTargets.YouTubeMusic;
    string account = CredentialTargets.AccountCookies;

    await store.SetCredentialAsync(service, account, "cookie_data", TestContext.Current.CancellationToken);
    await store.DeleteCredentialAsync(service, account, TestContext.Current.CancellationToken);

    string? result = await store.GetCredentialAsync(service, account, TestContext.Current.CancellationToken);
    Assert.Null(result);
  }

  [Fact]
  public async Task InMemory_IsAvailableAsync_ReturnsTrue() {
    var store = new InMemoryCredentialStore();
    bool available = await store.IsAvailableAsync(TestContext.Current.CancellationToken);
    Assert.True(available);
  }

  [Fact]
  public async Task InMemory_Cancellation_ThrowsOperationCanceledException() {
    var store = new InMemoryCredentialStore();
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        await store.GetCredentialAsync("svc", "acct", cts.Token));
  }

  [Fact]
  public async Task InMemory_NullArguments_ThrowArgumentNullException() {
    var store = new InMemoryCredentialStore();

    await Assert.ThrowsAsync<ArgumentNullException>(() =>
        store.GetCredentialAsync(null!, "acct", TestContext.Current.CancellationToken));
    await Assert.ThrowsAsync<ArgumentNullException>(() =>
        store.GetCredentialAsync("svc", null!, TestContext.Current.CancellationToken));
    await Assert.ThrowsAsync<ArgumentNullException>(() =>
        store.SetCredentialAsync("svc", "acct", null!, TestContext.Current.CancellationToken));
    await Assert.ThrowsAsync<ArgumentNullException>(() =>
        store.DeleteCredentialAsync(null!, "acct", TestContext.Current.CancellationToken));
  }

  #endregion

  #region EnvironmentCredentialStore Tests

  [Fact]
  public async Task Environment_GetAsync_ResolvesSubsonicPassword() {
    var env = new Dictionary<string, string> {
      ["SMOC_SUBSONIC_PASSWORD"] = "global_subsonic_pwd"
    };

    var store = new EnvironmentCredentialStore(key => env.GetValueOrDefault(key));
    string? result = await store.GetCredentialAsync(CredentialTargets.Subsonic, "razza", TestContext.Current.CancellationToken);

    Assert.Equal("global_subsonic_pwd", result);
  }

  [Fact]
  public async Task Environment_GetAsync_PrefersUserSpecificSubsonicPassword() {
    var env = new Dictionary<string, string> {
      ["SMOC_SUBSONIC_PASSWORD"] = "global_subsonic_pwd",
      ["SMOC_SUBSONIC_RAZZA_PASSWORD"] = "razza_specific_pwd"
    };

    var store = new EnvironmentCredentialStore(key => env.GetValueOrDefault(key));
    string? result = await store.GetCredentialAsync(CredentialTargets.Subsonic, "razza", TestContext.Current.CancellationToken);

    Assert.Equal("razza_specific_pwd", result);
  }

  [Fact]
  public async Task Environment_GetAsync_ResolvesSoundCloudToken() {
    var env = new Dictionary<string, string> {
      ["SMOC_SOUNDCLOUD_AUTH_TOKEN"] = "sc_auth_token_value"
    };

    var store = new EnvironmentCredentialStore(key => env.GetValueOrDefault(key));
    string? result = await store.GetCredentialAsync(CredentialTargets.SoundCloud, CredentialTargets.AccountAuthToken, TestContext.Current.CancellationToken);

    Assert.Equal("sc_auth_token_value", result);
  }

  [Fact]
  public async Task Environment_GetAsync_ResolvesYtmCookiesAndTokens() {
    var env = new Dictionary<string, string> {
      ["SMOC_YTM_COOKIES"] = "ytm_session_cookies",
      ["SMOC_YTM_TOKENS"] = "{\"poToken\":\"xyz\"}"
    };

    var store = new EnvironmentCredentialStore(key => env.GetValueOrDefault(key));
    string? cookies = await store.GetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, TestContext.Current.CancellationToken);
    string? tokens = await store.GetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, TestContext.Current.CancellationToken);

    Assert.Equal("ytm_session_cookies", cookies);
    Assert.Equal("{\"poToken\":\"xyz\"}", tokens);
  }

  [Fact]
  public async Task Environment_GetAsync_ResolvesGenericEnvVarFallback() {
    var env = new Dictionary<string, string> {
      ["SMOC_CREDENTIAL_SMOC_SPOTIFY_CLIENT_SECRET"] = "spotify_secret_123"
    };

    var store = new EnvironmentCredentialStore(key => env.GetValueOrDefault(key));
    string? result = await store.GetCredentialAsync("smoc:spotify", "client_secret", TestContext.Current.CancellationToken);

    Assert.Equal("spotify_secret_123", result);
  }

  [Fact]
  public async Task Environment_SetAndClear_InvokesSetEnvCallback() {
    var env = new Dictionary<string, string>();
    var store = new EnvironmentCredentialStore(
        key => env.GetValueOrDefault(key),
        (key, val) => {
          if (val == null) env.Remove(key);
          else env[key] = val;
        });

    await store.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, "new_cookie", TestContext.Current.CancellationToken);
    Assert.Equal("new_cookie", env["SMOC_YTM_COOKIES"]);

    await store.DeleteCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, TestContext.Current.CancellationToken);
    Assert.False(env.ContainsKey("SMOC_YTM_COOKIES"));
  }

  #endregion

  #region CompositeCredentialStore Tests

  [Fact]
  public async Task Composite_GetAsync_PrefersPrimaryWhenAvailable() {
    var primary = new InMemoryCredentialStore();
    var fallback = new InMemoryCredentialStore();

    await primary.SetCredentialAsync("svc", "acct", "primary_val", TestContext.Current.CancellationToken);
    await fallback.SetCredentialAsync("svc", "acct", "fallback_val", TestContext.Current.CancellationToken);

    var composite = new CompositeCredentialStore(primary, fallback);
    string? result = await composite.GetCredentialAsync("svc", "acct", TestContext.Current.CancellationToken);

    Assert.Equal("primary_val", result);
  }

  [Fact]
  public async Task Composite_GetAsync_FallsBackWhenPrimaryNull() {
    var primary = new InMemoryCredentialStore();
    var fallback = new InMemoryCredentialStore();

    await fallback.SetCredentialAsync("svc", "acct", "fallback_val", TestContext.Current.CancellationToken);

    var composite = new CompositeCredentialStore(primary, fallback);
    string? result = await composite.GetCredentialAsync("svc", "acct", TestContext.Current.CancellationToken);

    Assert.Equal("fallback_val", result);
  }

  [Fact]
  public async Task Composite_GetAsync_FallsBackWhenPrimaryUnavailable() {
    var unavailableMock = new Mock<ICredentialStore>();
    unavailableMock.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

    var fallback = new InMemoryCredentialStore();
    await fallback.SetCredentialAsync("svc", "acct", "fallback_val", TestContext.Current.CancellationToken);

    var composite = new CompositeCredentialStore(unavailableMock.Object, fallback);
    string? result = await composite.GetCredentialAsync("svc", "acct", TestContext.Current.CancellationToken);

    Assert.Equal("fallback_val", result);
    unavailableMock.Verify(s => s.GetCredentialAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task Composite_SetAsync_WritesToPrimaryWhenAvailable() {
    var primary = new InMemoryCredentialStore();
    var fallback = new InMemoryCredentialStore();

    var composite = new CompositeCredentialStore(primary, fallback);
    await composite.SetCredentialAsync("svc", "acct", "value", TestContext.Current.CancellationToken);

    string? primaryVal = await primary.GetCredentialAsync("svc", "acct", TestContext.Current.CancellationToken);
    string? fallbackVal = await fallback.GetCredentialAsync("svc", "acct", TestContext.Current.CancellationToken);

    Assert.Equal("value", primaryVal);
    Assert.Null(fallbackVal);
  }

  [Fact]
  public async Task Composite_SetAsync_WritesToFallbackWhenPrimaryUnavailable() {
    var unavailableMock = new Mock<ICredentialStore>();
    unavailableMock.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

    var fallback = new InMemoryCredentialStore();

    var composite = new CompositeCredentialStore(unavailableMock.Object, fallback);
    await composite.SetCredentialAsync("svc", "acct", "value", TestContext.Current.CancellationToken);

    string? fallbackVal = await fallback.GetCredentialAsync("svc", "acct", TestContext.Current.CancellationToken);
    Assert.Equal("value", fallbackVal);
  }

  [Fact]
  public async Task Composite_DeleteAsync_DeletesFromAllAvailableStores() {
    var primary = new InMemoryCredentialStore();
    var fallback = new InMemoryCredentialStore();

    await primary.SetCredentialAsync("svc", "acct", "val1", TestContext.Current.CancellationToken);
    await fallback.SetCredentialAsync("svc", "acct", "val2", TestContext.Current.CancellationToken);

    var composite = new CompositeCredentialStore(primary, fallback);
    await composite.DeleteCredentialAsync("svc", "acct", TestContext.Current.CancellationToken);

    Assert.Null(await primary.GetCredentialAsync("svc", "acct", TestContext.Current.CancellationToken));
    Assert.Null(await fallback.GetCredentialAsync("svc", "acct", TestContext.Current.CancellationToken));
  }

  #endregion

  #region LinuxSecretServiceCredentialStore Tests

  [Fact]
  public async Task LinuxSecretService_GetCredentialAsync_DelegatesToAdapter() {
    var mockAdapter = new Mock<ISecretServiceAdapter>();
    mockAdapter.Setup(a => a.GetSecretAsync("smoc:subsonic", "razza", It.IsAny<CancellationToken>()))
               .ReturnsAsync("subsonic_pass");

    var store = new LinuxSecretServiceCredentialStore(mockAdapter.Object);
    string? result = await store.GetCredentialAsync("smoc:subsonic", "razza", TestContext.Current.CancellationToken);

    Assert.Equal("subsonic_pass", result);
  }

  [Fact]
  public async Task LinuxSecretService_SetCredentialAsync_DelegatesToAdapter() {
    var mockAdapter = new Mock<ISecretServiceAdapter>();
    var store = new LinuxSecretServiceCredentialStore(mockAdapter.Object);

    await store.SetCredentialAsync("smoc:subsonic", "razza", "my_pass", TestContext.Current.CancellationToken);

    mockAdapter.Verify(a => a.SetSecretAsync("smoc:subsonic", "razza", "my_pass", It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task LinuxSecretService_DeleteCredentialAsync_DelegatesToAdapter() {
    var mockAdapter = new Mock<ISecretServiceAdapter>();
    var store = new LinuxSecretServiceCredentialStore(mockAdapter.Object);

    await store.DeleteCredentialAsync("smoc:subsonic", "razza", TestContext.Current.CancellationToken);

    mockAdapter.Verify(a => a.DeleteSecretAsync("smoc:subsonic", "razza", It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task LinuxSecretService_IsAvailableAsync_DelegatesToAdapter() {
    var mockAdapter = new Mock<ISecretServiceAdapter>();
    mockAdapter.Setup(a => a.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

    var store = new LinuxSecretServiceCredentialStore(mockAdapter.Object);
    bool available = await store.IsAvailableAsync(TestContext.Current.CancellationToken);

    Assert.True(available);
  }

  #endregion

  #region MacOsKeychainCredentialStore Tests

  [Fact]
  public async Task MacOsKeychain_GetCredentialAsync_DelegatesToAdapter() {
    var mockAdapter = new Mock<IMacOsKeychainAdapter>();
    mockAdapter.SetupGet(a => a.IsSupportedPlatform).Returns(true);
    mockAdapter.Setup(a => a.GetSecretAsync("smoc:soundcloud", "token", It.IsAny<CancellationToken>()))
               .ReturnsAsync("mac_sc_token");

    var store = new MacOsKeychainCredentialStore(mockAdapter.Object);
    string? result = await store.GetCredentialAsync("smoc:soundcloud", "token", TestContext.Current.CancellationToken);

    Assert.Equal("mac_sc_token", result);
    Assert.True(await store.IsAvailableAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task MacOsKeychain_SetAndDelete_DelegatesToAdapter() {
    var mockAdapter = new Mock<IMacOsKeychainAdapter>();
    mockAdapter.SetupGet(a => a.IsSupportedPlatform).Returns(true);

    var store = new MacOsKeychainCredentialStore(mockAdapter.Object);
    await store.SetCredentialAsync("smoc:subsonic", "user", "pass", TestContext.Current.CancellationToken);
    await store.DeleteCredentialAsync("smoc:subsonic", "user", TestContext.Current.CancellationToken);

    mockAdapter.Verify(a => a.SetSecretAsync("smoc:subsonic", "user", "pass", It.IsAny<CancellationToken>()), Times.Once);
    mockAdapter.Verify(a => a.DeleteSecretAsync("smoc:subsonic", "user", It.IsAny<CancellationToken>()), Times.Once);
  }

  #endregion

  #region WindowsCredentialManagerCredentialStore Tests

  [Fact]
  public async Task WindowsStore_SmallCredential_WritesToCredentialManager() {
    string tempDir = Path.Combine(Path.GetTempPath(), "smoc_test_win_" + Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(tempDir);
    try {
      var mockAdapter = new Mock<IWindowsCredentialAdapter>();
      mockAdapter.SetupGet(a => a.IsSupportedPlatform).Returns(true);
      mockAdapter.SetupGet(a => a.CredentialsDirectory).Returns(tempDir);
      mockAdapter.Setup(a => a.WriteCredential(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                 .Returns(true);

      var store = new WindowsCredentialManagerCredentialStore(mockAdapter.Object);
      string smallSecret = "standard_password";

      await store.SetCredentialAsync("smoc:subsonic", "razza", smallSecret, TestContext.Current.CancellationToken);

      mockAdapter.Verify(a => a.WriteCredential("smoc:subsonic:razza", "razza", smallSecret), Times.Once);
      mockAdapter.Verify(a => a.ProtectData(It.IsAny<byte[]>()), Times.Never);
    } finally {
      if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }
  }

  [Fact]
  public async Task WindowsStore_LargePayload_WritesToDpapiFile() {
    string tempDir = Path.Combine(Path.GetTempPath(), "smoc_test_win_" + Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(tempDir);
    try {
      var mockAdapter = new Mock<IWindowsCredentialAdapter>();
      mockAdapter.SetupGet(a => a.IsSupportedPlatform).Returns(true);
      mockAdapter.SetupGet(a => a.CredentialsDirectory).Returns(tempDir);
      mockAdapter.Setup(a => a.ProtectData(It.IsAny<byte[]>()))
                 .Returns<byte[]>(b => b); // Identity "encryption" for test

      var store = new WindowsCredentialManagerCredentialStore(mockAdapter.Object);
      // Large secret > 2048 bytes (e.g. 3500 bytes YTM cookie string)
      string largeCookies = new('A', 3500);

      await store.SetCredentialAsync("smoc:youtubemusic", "cookies", largeCookies, TestContext.Current.CancellationToken);

      mockAdapter.Verify(a => a.ProtectData(It.IsAny<byte[]>()), Times.Once);
      mockAdapter.Verify(a => a.WriteCredential(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
      mockAdapter.Verify(a => a.DeleteCredential("smoc:youtubemusic:cookies"), Times.Once);

      // Verify file was written to credentials directory
      string[] files = Directory.GetFiles(tempDir, "*.dat");
      Assert.Single(files);
    } finally {
      if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }
  }

  [Fact]
  public async Task WindowsStore_GetCredential_PrefersDpapiFileWhenPresent() {
    string tempDir = Path.Combine(Path.GetTempPath(), "smoc_test_win_" + Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(tempDir);
    try {
      var mockAdapter = new Mock<IWindowsCredentialAdapter>();
      mockAdapter.SetupGet(a => a.IsSupportedPlatform).Returns(true);
      mockAdapter.SetupGet(a => a.CredentialsDirectory).Returns(tempDir);
      mockAdapter.Setup(a => a.ProtectData(It.IsAny<byte[]>()))
                 .Returns<byte[]>(b => b);
      mockAdapter.Setup(a => a.UnprotectData(It.IsAny<byte[]>()))
                 .Returns<byte[]>(b => b);

      var store = new WindowsCredentialManagerCredentialStore(mockAdapter.Object);
      string largeCookies = new('C', 2500);

      await store.SetCredentialAsync("smoc:youtubemusic", "cookies", largeCookies, TestContext.Current.CancellationToken);

      string? retrieved = await store.GetCredentialAsync("smoc:youtubemusic", "cookies", TestContext.Current.CancellationToken);
      Assert.Equal(largeCookies, retrieved);
      mockAdapter.Verify(a => a.UnprotectData(It.IsAny<byte[]>()), Times.Once);
    } finally {
      if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }
  }

  [Fact]
  public async Task WindowsStore_DeleteCredential_CleansUpBothCredentialManagerAndDpapi() {
    string tempDir = Path.Combine(Path.GetTempPath(), "smoc_test_win_" + Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(tempDir);
    try {
      var mockAdapter = new Mock<IWindowsCredentialAdapter>();
      mockAdapter.SetupGet(a => a.IsSupportedPlatform).Returns(true);
      mockAdapter.SetupGet(a => a.CredentialsDirectory).Returns(tempDir);
      mockAdapter.Setup(a => a.ProtectData(It.IsAny<byte[]>())).Returns<byte[]>(b => b);

      var store = new WindowsCredentialManagerCredentialStore(mockAdapter.Object);
      await store.SetCredentialAsync("smoc:youtubemusic", "cookies", new string('X', 3000), TestContext.Current.CancellationToken);

      await store.DeleteCredentialAsync("smoc:youtubemusic", "cookies", TestContext.Current.CancellationToken);

      mockAdapter.Verify(a => a.DeleteCredential("smoc:youtubemusic:cookies"), Times.AtLeastOnce());
      string[] files = Directory.GetFiles(tempDir, "*.dat");
      Assert.Empty(files);
    } finally {
      if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }
  }

  #endregion

  #region CredentialStoreFactory Tests

  [Fact]
  public void Factory_CreateInMemory_ReturnsFunctionalStore() {
    InMemoryCredentialStore store = CredentialStoreFactory.CreateInMemory();
    Assert.NotNull(store);
  }

  [Fact]
  public void Factory_CreateNativeStore_ReturnsStoreForCurrentPlatform() {
    ICredentialStore store = CredentialStoreFactory.CreateNativeStore();
    Assert.NotNull(store);

    if (OperatingSystem.IsLinux()) {
      Assert.IsType<LinuxSecretServiceCredentialStore>(store);
    } else if (OperatingSystem.IsMacOS()) {
      Assert.IsType<MacOsKeychainCredentialStore>(store);
    } else if (OperatingSystem.IsWindows()) {
      Assert.IsType<WindowsCredentialManagerCredentialStore>(store);
    }
  }

  [Fact]
  public void Factory_CreateDefault_ReturnsCompositeStoreWithNativeAndEnv() {
    ICredentialStore defaultStore = CredentialStoreFactory.CreateDefault();
    var composite = Assert.IsType<CompositeCredentialStore>(defaultStore);

    Assert.Equal(2, composite.Stores.Count);
    Assert.IsType<EnvironmentCredentialStore>(composite.Stores[1]);
  }

  #endregion
}
