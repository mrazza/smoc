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
  public async Task Composite_GetCredentialWithSourceAsync_ReturnsMatchingStore() {
    var primary = new InMemoryCredentialStore();
    var fallback = new InMemoryCredentialStore();

    await fallback.SetCredentialAsync("svc", "acct", "fallback_val", TestContext.Current.CancellationToken);

    var composite = new CompositeCredentialStore(primary, fallback);
    var (value, source) = await composite.GetCredentialWithSourceAsync("svc", "acct", TestContext.Current.CancellationToken);

    Assert.Equal("fallback_val", value);
    Assert.Same(fallback, source);
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
  public async Task Composite_DeleteAsync_ThrowsWhenAnyWritableStoreFails() {
    var failingPrimary = new Mock<ICredentialStore>();
    failingPrimary.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
    failingPrimary.Setup(s => s.DeleteCredentialAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new InvalidOperationException("Keyring locked"));

    var succeedingSecondary = new InMemoryCredentialStore();
    var composite = new CompositeCredentialStore(failingPrimary.Object, succeedingSecondary);

    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        composite.DeleteCredentialAsync("svc", "acct", TestContext.Current.CancellationToken));
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
  public async Task Composite_GetAsync_ThrowsWhenPrimaryStoreFailsEvenIfFallbackStoreReturnsNull() {
    var failingStore = new Mock<ICredentialStore>();
    failingStore.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
    failingStore.Setup(s => s.GetCredentialAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Keyring is locked"));

    var emptyStore = new InMemoryCredentialStore(); // Returns null for unset credentials

    var composite = new CompositeCredentialStore(failingStore.Object, emptyStore);
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        composite.GetCredentialAsync("svc", "acct", TestContext.Current.CancellationToken));

    Assert.Contains("Keyring is locked", ex.InnerException?.Message);
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

  [Fact]
  public async Task Composite_SetAsync_ThrowsPrimaryFailureWhenFallbackIsReadOnly() {
    var failingPrimary = new Mock<ICredentialStore>();
    failingPrimary.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
    var primaryException = new InvalidOperationException("Keyring is locked");
    failingPrimary.Setup(s => s.SetCredentialAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(primaryException);

    var readOnlyFallback = new EnvironmentCredentialStore();

    var composite = new CompositeCredentialStore(failingPrimary.Object, readOnlyFallback);
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        composite.SetCredentialAsync("svc", "acct", "secret", TestContext.Current.CancellationToken));

    Assert.Same(primaryException, ex.InnerException);
  }

  [Fact]
  public async Task Composite_SetAsync_ThrowsWhenPrimaryUnavailableAndFallbackIsReadOnly() {
    var unavailablePrimary = new Mock<ICredentialStore>();
    unavailablePrimary.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

    var readOnlyFallback = new EnvironmentCredentialStore();

    var composite = new CompositeCredentialStore(unavailablePrimary.Object, readOnlyFallback);
    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        composite.SetCredentialAsync("svc", "acct", "secret", TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Composite_IsAvailableAsync_ReturnsTrueWhenAnyStoreAvailable() {
    var unavailable = new Mock<ICredentialStore>();
    unavailable.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

    var available = new Mock<ICredentialStore>();
    available.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

    var composite = new CompositeCredentialStore(unavailable.Object, available.Object);
    bool result = await composite.IsAvailableAsync(TestContext.Current.CancellationToken);

    Assert.True(result);
  }

  [Fact]
  public async Task Composite_IsAvailableAsync_ReturnsFalseWhenNoStoreAvailable() {
    var unavailable1 = new Mock<ICredentialStore>();
    unavailable1.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

    var unavailable2 = new Mock<ICredentialStore>();
    unavailable2.Setup(s => s.IsAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

    var composite = new CompositeCredentialStore(unavailable1.Object, unavailable2.Object);
    bool result = await composite.IsAvailableAsync(TestContext.Current.CancellationToken);

    Assert.False(result);
  }

  [Fact]
  public async Task Composite_IsAvailableAsync_CachesTrueButRetriesAfterFalse() {
    var store = new Mock<ICredentialStore>();
    store.SetupSequence(s => s.IsAvailableAsync(It.IsAny<CancellationToken>()))
         .ReturnsAsync(false)
         .ReturnsAsync(true);

    var composite = new CompositeCredentialStore(store.Object);

    Assert.False(await composite.IsAvailableAsync(TestContext.Current.CancellationToken));
    Assert.True(await composite.IsAvailableAsync(TestContext.Current.CancellationToken));
    Assert.True(await composite.IsAvailableAsync(TestContext.Current.CancellationToken));

    store.Verify(s => s.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
  }

  [Fact]
  public void Composite_Constructor_DefensivelyCopiesStoresArray() {
    ICredentialStore s1 = new InMemoryCredentialStore();
    ICredentialStore s2 = new InMemoryCredentialStore();
    ICredentialStore[] array = [s1];

    var composite = new CompositeCredentialStore(array);
    array[0] = s2;

    Assert.Same(s1, composite.Stores[0]);
  }

  [Fact]
  public async Task Composite_PropagatesCancellationToken() {
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    var composite = new CompositeCredentialStore(new InMemoryCredentialStore());

    await Assert.ThrowsAsync<OperationCanceledException>(() =>
        composite.GetCredentialAsync("svc", "acct", cts.Token));

    await Assert.ThrowsAsync<OperationCanceledException>(() =>
        composite.SetCredentialAsync("svc", "acct", "val", cts.Token));

    await Assert.ThrowsAsync<OperationCanceledException>(() =>
        composite.DeleteCredentialAsync("svc", "acct", cts.Token));

    await Assert.ThrowsAsync<OperationCanceledException>(() =>
        composite.IsAvailableAsync(cts.Token));
  }
}
