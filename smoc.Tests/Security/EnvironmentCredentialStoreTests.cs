using Smoc.Security;
using Xunit;

namespace smoc.Tests.Security;

public class EnvironmentCredentialStoreTests {
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
      ["SMOC_CREDENTIAL_SMOC_CUSTOM_CLIENT_SECRET"] = "custom_secret_123"
    };

    var store = new EnvironmentCredentialStore(key => env.GetValueOrDefault(key));
    string? result = await store.GetCredentialAsync("smoc:custom", "client_secret", TestContext.Current.CancellationToken);

    Assert.Equal("custom_secret_123", result);
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

  [Fact]
  public async Task Environment_ReadOnly_ThrowsNotSupportedExceptionOnSet() {
    var storeDefault = new EnvironmentCredentialStore();
    var storeExplicitNull = new EnvironmentCredentialStore(_ => null, setEnv: null);

    await Assert.ThrowsAsync<NotSupportedException>(() =>
        storeDefault.SetCredentialAsync(CredentialTargets.Subsonic, "razza", "secret", TestContext.Current.CancellationToken));

    await Assert.ThrowsAsync<NotSupportedException>(() =>
        storeExplicitNull.SetCredentialAsync(CredentialTargets.Subsonic, "razza", "secret", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Environment_ReadOnly_ThrowsNotSupportedExceptionOnDelete() {
    var storeDefault = new EnvironmentCredentialStore();
    var storeExplicitNull = new EnvironmentCredentialStore(_ => null, setEnv: null);

    await Assert.ThrowsAsync<NotSupportedException>(() =>
        storeDefault.DeleteCredentialAsync(CredentialTargets.Subsonic, "razza", TestContext.Current.CancellationToken));

    await Assert.ThrowsAsync<NotSupportedException>(() =>
        storeExplicitNull.DeleteCredentialAsync(CredentialTargets.Subsonic, "razza", TestContext.Current.CancellationToken));
  }
}
