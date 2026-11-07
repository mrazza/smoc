using System.Net;
using System.Text.Json;
using Smoc.Security;
using Smoc.Streaming.YouTubeMusic;
using Xunit;

namespace smoc.Tests.Streaming.YouTubeMusic;

public class YtmStreamingClientTests {
  [Fact]
  public void ParseCookies_ValidCookieString_ParsesExpectedCookies() {
    string cookieHeader = "SID=abc123; HSID=def456; SSID=ghi789;";

    List<Cookie> cookies = YtmStreamingClient.ParseCookies(cookieHeader);

    Assert.Equal(3, cookies.Count);
    Assert.Equal("SID", cookies[0].Name);
    Assert.Equal("abc123", cookies[0].Value);
    Assert.Equal(".youtube.com", cookies[0].Domain);
    Assert.Equal("/", cookies[0].Path);

    Assert.Equal("HSID", cookies[1].Name);
    Assert.Equal("def456", cookies[1].Value);

    Assert.Equal("SSID", cookies[2].Name);
    Assert.Equal("ghi789", cookies[2].Value);
  }

  [Fact]
  public void ParseCookies_WhitespaceAndMalformedEntries_HandlesGracefully() {
    string cookieHeader = "  SID = abc123 ; ; malformed_no_equals ; HSID=xyz = extra ;   ";

    List<Cookie> cookies = YtmStreamingClient.ParseCookies(cookieHeader);

    Assert.Equal(2, cookies.Count);
    Assert.Equal("SID", cookies[0].Name);
    Assert.Equal("abc123", cookies[0].Value);

    Assert.Equal("HSID", cookies[1].Name);
    Assert.Equal("xyz = extra", cookies[1].Value);
  }

  [Fact]
  public void ParseCookies_EmptyString_ReturnsEmptyList() {
    List<Cookie> cookies = YtmStreamingClient.ParseCookies(string.Empty);

    Assert.Empty(cookies);
  }

  [Fact]
  public async Task CreateAsync_ResolvesCookiesAndTokensFromCredentialStore() {
    var store = new InMemoryCredentialStore();
    await store.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, "SID=abc123; HSID=def456;", TestContext.Current.CancellationToken);
    var tokens = new YtmTokens("poVal", "rolloutVal", "visitorVal");
    await store.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, JsonSerializer.Serialize(tokens), TestContext.Current.CancellationToken);

    var client = await YtmStreamingClient.CreateAsync(credentialStore: store, cancellationToken: TestContext.Current.CancellationToken);

    Assert.NotNull(client);
    Assert.True(client.IsAuthenticated);
  }

  [Fact]
  public async Task CreateAsync_GeneratesTokensAndSavesToStore_WhenTokensMissingInStore() {
    var store = new InMemoryCredentialStore();
    await store.SetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountCookies, "SID=abc123; HSID=def456;", TestContext.Current.CancellationToken);

    bool tokenGeneratorCalled = false;
    Task<YtmTokens> MockTokenGenerator(List<Cookie> cookies) {
      tokenGeneratorCalled = true;
      return Task.FromResult(new YtmTokens("generatedPo", "generatedRollout", "generatedVisitor"));
    }

    var client = await YtmStreamingClient.CreateAsync(credentialStore: store, tokenGenerator: MockTokenGenerator, cancellationToken: TestContext.Current.CancellationToken);

    Assert.NotNull(client);
    Assert.True(client.IsAuthenticated);
    Assert.True(tokenGeneratorCalled);

    string? savedTokensJson = await store.GetCredentialAsync(CredentialTargets.YouTubeMusic, CredentialTargets.AccountTokens, TestContext.Current.CancellationToken);
    Assert.NotNull(savedTokensJson);
    var savedTokens = JsonSerializer.Deserialize<YtmTokens>(savedTokensJson);
    Assert.Equal("generatedPo", savedTokens?.PoToken);
  }

  [Fact]
  public async Task CreateAsync_FallsBackToLegacyCookiesAndTokensFiles_WhenNotInStore() {
    string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDir);
    string cookiesFile = Path.Combine(tempDir, "cookies.txt");
    string tokensFile = Path.Combine(tempDir, "tokens.json");

    try {
      await File.WriteAllTextAsync(cookiesFile, "SID=fileCookie123; HSID=fileCookie456;", TestContext.Current.CancellationToken);
      var tokens = new YtmTokens("filePo", "fileRollout", "fileVisitor");
      await File.WriteAllTextAsync(tokensFile, JsonSerializer.Serialize(tokens), TestContext.Current.CancellationToken);

      var store = new InMemoryCredentialStore();
      var client = await YtmStreamingClient.CreateAsync(
        credentialStore: store,
        cookiesPath: cookiesFile,
        tokensPath: tokensFile,
        cancellationToken: TestContext.Current.CancellationToken
      );

      Assert.NotNull(client);
      Assert.True(client.IsAuthenticated);
    } finally {
      Directory.Delete(tempDir, true);
    }
  }

  [Fact]
  public async Task CreateAsync_ReturnsUnauthenticated_WhenNoStoreOrFileCredentials() {
    var store = new InMemoryCredentialStore();
    var client = await YtmStreamingClient.CreateAsync(
      credentialStore: store,
      cookiesPath: "/tmp/nonexistent_cookies_" + Guid.NewGuid().ToString("N"),
      tokensPath: "/tmp/nonexistent_tokens_" + Guid.NewGuid().ToString("N"),
      cancellationToken: TestContext.Current.CancellationToken
    );

    Assert.NotNull(client);
    Assert.False(client.IsAuthenticated);
  }
}