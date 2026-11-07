using Moq;
using Smoc.Security;
using Xunit;

namespace smoc.Tests.Security;

public class CompositeCredentialStoreTests {
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

  [Fact]
  public async Task Composite_SetAsync_ThrowsWhenNoStoreSucceeds() {
    var unavailableMock = new Mock<ICredentialStore>();
    unavailableMock.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

    var composite = new CompositeCredentialStore(unavailableMock.Object);
    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        composite.SetCredentialAsync("svc", "acct", "val", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Composite_DeleteAsync_ThrowsWhenNoStoreSucceeds() {
    var unavailableMock = new Mock<ICredentialStore>();
    unavailableMock.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

    var composite = new CompositeCredentialStore(unavailableMock.Object);
    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        composite.DeleteCredentialAsync("svc", "acct", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Composite_GetAsync_ThrowsWhenAllStoresThrowException() {
    var failingStore1 = new Mock<ICredentialStore>();
    failingStore1.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
    failingStore1.Setup(s => s.GetCredentialAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("Store 1 failed"));

    var failingStore2 = new Mock<ICredentialStore>();
    failingStore2.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
    failingStore2.Setup(s => s.GetCredentialAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("Store 2 failed"));

    var composite = new CompositeCredentialStore(failingStore1.Object, failingStore2.Object);
    await Assert.ThrowsAsync<AggregateException>(() =>
        composite.GetCredentialAsync("svc", "acct", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Composite_GetAsync_ReturnsNullWhenOneStoreSucceedsWithNullEvenIfAnotherStoreThrows() {
    var failingStore = new Mock<ICredentialStore>();
    failingStore.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
    failingStore.Setup(s => s.GetCredentialAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Store 1 failed"));

    var emptyStore = new InMemoryCredentialStore(); // Returns null for unset credentials

    var composite = new CompositeCredentialStore(failingStore.Object, emptyStore);
    string? result = await composite.GetCredentialAsync("svc", "acct", TestContext.Current.CancellationToken);

    Assert.Null(result);
  }

  [Fact]
  public async Task Composite_GetAsync_ReturnsValueWhenOneStoreSucceedsEvenIfFirstStoreThrows() {
    var failingStore = new Mock<ICredentialStore>();
    failingStore.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
    failingStore.Setup(s => s.GetCredentialAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Store 1 failed"));

    var populatedStore = new InMemoryCredentialStore();
    await populatedStore.SetCredentialAsync("svc", "acct", "secret_value", TestContext.Current.CancellationToken);

    var composite = new CompositeCredentialStore(failingStore.Object, populatedStore);
    string? result = await composite.GetCredentialAsync("svc", "acct", TestContext.Current.CancellationToken);

    Assert.Equal("secret_value", result);
  }
}
