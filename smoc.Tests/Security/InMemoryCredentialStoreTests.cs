using Smoc.Security;
using Xunit;

namespace smoc.Tests.Security;

public class InMemoryCredentialStoreTests {
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
}
