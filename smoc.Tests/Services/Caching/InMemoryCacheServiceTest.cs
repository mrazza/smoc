using System.Text;
using Smoc.Services.Caching;

namespace smoc.Tests.Services.Caching;

public class InMemoryCacheServiceTest {

  [Fact]
  public async Task GetOrAddAsync_OnCacheMiss_CallsFactoryAndCaches() {
    var config = new CacheConfig();
    var service = new InMemoryCacheService(config);
    string key = "test-key";
    string expectedContent = "hello world";

    using var resultStream = await service.GetOrAddAsync(
        key,
        _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(expectedContent))),
        TestContext.Current.CancellationToken);

    Assert.NotNull(resultStream);
    using var reader = new StreamReader(resultStream);
    string content = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    Assert.Equal(expectedContent, content);
  }

  [Fact]
  public async Task GetOrAddAsync_OnCacheHit_ReturnsCachedValue() {
    var config = new CacheConfig();
    var service = new InMemoryCacheService(config);
    string key = "test-key";
    string expectedContent = "cached content";
    bool factoryCalled = false;

    // Pre-populate cache
    await service.GetOrAddAsync(
        key,
        _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(expectedContent))),
        TestContext.Current.CancellationToken);

    using var resultStream = await service.GetOrAddAsync(key, (ct) => {
      factoryCalled = true;
      return Task.FromResult<Stream>(new MemoryStream());
    }, TestContext.Current.CancellationToken);

    Assert.False(factoryCalled, "Factory should not be called on cache hit");
    using var reader = new StreamReader(resultStream);
    string content = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    Assert.Equal(expectedContent, content);
  }

  [Fact]
  public async Task Evict_LRU_RespectsMaxElements() {
    int maxElements = 2;
    var config = new CacheConfig(MaxElements: maxElements, EvictionStrategy: EvictionStrategy.LRU);
    var service = new InMemoryCacheService(config);

    string key1 = "key1";
    string key2 = "key2";
    string key3 = "key3";

    await service.GetOrAddAsync(key1, _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("content1"))), TestContext.Current.CancellationToken);
    await service.GetOrAddAsync(key2, _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("content2"))), TestContext.Current.CancellationToken);
    await service.GetOrAddAsync(key3, _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("content3"))), TestContext.Current.CancellationToken);

    // Access key1 to make it recently used, key2 will be least recently used (if we consider insertion order as usage, but let's be explicit)
    // Wait a bit to ensure timestamps are different if resolution is low, or just rely on order.
    await Task.Delay(10);
    await service.GetOrAddAsync(key1, _ => Task.FromResult<Stream>(new MemoryStream()), TestContext.Current.CancellationToken);

    await service.Evict(TestContext.Current.CancellationToken);

    bool k1Exists = await IsKeyInCache(service, key1);
    bool k2Exists = await IsKeyInCache(service, key2);
    bool k3Exists = await IsKeyInCache(service, key3);

    Assert.True(k1Exists, "Key1 was accessed recently, should be kept.");
    Assert.True(k3Exists, "Key3 is 2nd most recent, should be kept (Max=2).");
    Assert.False(k2Exists, "Key2 is oldest, should be evicted.");
  }

  [Fact]
  public async Task Evict_LargestFirst_RespectsMaxElements() {
    int maxElements = 2;
    var config = new CacheConfig(MaxElements: maxElements, EvictionStrategy: EvictionStrategy.LARGEST_FIRST);
    var service = new InMemoryCacheService(config);

    string smallKey = "small";
    string mediumKey = "medium";
    string largeKey = "large";

    await service.GetOrAddAsync(smallKey, _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("s"))), TestContext.Current.CancellationToken);
    await service.GetOrAddAsync(mediumKey, _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("mm"))), TestContext.Current.CancellationToken);
    await service.GetOrAddAsync(largeKey, _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("lll"))), TestContext.Current.CancellationToken);

    await service.Evict(TestContext.Current.CancellationToken);

    Assert.False(await IsKeyInCache(service, largeKey), "Largest file should be evicted");
    Assert.True(await IsKeyInCache(service, mediumKey), "Medium file should be kept");
    Assert.True(await IsKeyInCache(service, smallKey), "Small file should be kept");
  }

  [Fact]
  public async Task Evict_SmallestFirst_RespectsMaxElements() {
    int maxElements = 2;
    var config = new CacheConfig(MaxElements: maxElements, EvictionStrategy: EvictionStrategy.SMALLEST_FIRST);
    var service = new InMemoryCacheService(config);

    string smallKey = "small";
    string mediumKey = "medium";
    string largeKey = "large";

    await service.GetOrAddAsync(smallKey, _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("s"))), TestContext.Current.CancellationToken);
    await service.GetOrAddAsync(mediumKey, _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("mm"))), TestContext.Current.CancellationToken);
    await service.GetOrAddAsync(largeKey, _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("lll"))), TestContext.Current.CancellationToken);

    await service.Evict(TestContext.Current.CancellationToken);

    Assert.False(await IsKeyInCache(service, smallKey), "Smallest file should be evicted");
    Assert.True(await IsKeyInCache(service, mediumKey), "Medium file should be kept");
    Assert.True(await IsKeyInCache(service, largeKey), "Large file should be kept");
  }

  [Fact]
  public async Task Evict_LRU_RespectsMaxSizeBytes() {
    long maxSizeBytes = 2;
    // With maxSizeBytes=2, we can hold "a" (1) + "b" (1). "c" (1) would make it 3, so one must go.
    var config = new CacheConfig(MaxSizeBytes: maxSizeBytes, EvictionStrategy: EvictionStrategy.LRU);
    var service = new InMemoryCacheService(config);

    string key1 = "key1";
    string key2 = "key2";
    string key3 = "key3";

    await service.GetOrAddAsync(key1, _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("a"))), TestContext.Current.CancellationToken);
    await Task.Delay(10);
    await service.GetOrAddAsync(key2, _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("b"))), TestContext.Current.CancellationToken);
    await Task.Delay(10);
    await service.GetOrAddAsync(key3, _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("c"))), TestContext.Current.CancellationToken);

    await service.Evict(TestContext.Current.CancellationToken);

    Assert.True(await IsKeyInCache(service, key3), "Newest file should be kept");
    Assert.True(await IsKeyInCache(service, key2), "Second file fits in limit (1+1 <= 2)");
    Assert.False(await IsKeyInCache(service, key1), "Oldest file should be evicted because 1+1+1 > 2");
  }

  [Fact]
  public async Task Evict_NoLimits_DoesNothing() {
    var config = new CacheConfig(MaxElements: null, MaxSizeBytes: null);
    var service = new InMemoryCacheService(config);

    string key1 = "key1";
    await service.GetOrAddAsync(key1, _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("content"))), TestContext.Current.CancellationToken);

    await service.Evict(TestContext.Current.CancellationToken);

    Assert.True(await IsKeyInCache(service, key1));
  }

  [Fact]
  public async Task GetOrAddAsync_IsThreadSafe_SerializesFactoryExecution() {
    var config = new CacheConfig();
    var service = new InMemoryCacheService(config);
    var tcsFactoryStarted = new TaskCompletionSource();
    var tcsAllowFactoryToComplete = new TaskCompletionSource();

    // Task 1: Acquires lock and waits
    var task1 = service.GetOrAddAsync("key1", async _ => {
      tcsFactoryStarted.SetResult();
      await tcsAllowFactoryToComplete.Task;
      return new MemoryStream();
    }, TestContext.Current.CancellationToken);

    // Wait for Task 1 to enter the factory (holding the lock)
    await tcsFactoryStarted.Task;

    // Task 2: Try to acquire lock
    bool task2FactoryCalled = false;
    var task2 = service.GetOrAddAsync("key2", _ => {
      task2FactoryCalled = true;
      return Task.FromResult<Stream>(new MemoryStream());
    }, TestContext.Current.CancellationToken);

    // Give Task 2 a moment to potentially run (it shouldn't)
    await Task.Delay(50);
    Assert.False(task2FactoryCalled, "Task 2 should be blocked while Task 1 holds the lock.");
    Assert.False(task2.IsCompleted, "Task 2 should not complete yet.");

    // Release Task 1
    tcsAllowFactoryToComplete.SetResult();
    await task1;
    await task2;

    Assert.True(task2FactoryCalled, "Task 2 should assume lock after Task 1 releases it.");
  }

  [Fact]
  public async Task Evict_IsThreadSafe_WaitsForGetOrAdd() {
    var config = new CacheConfig();
    var service = new InMemoryCacheService(config);
    var tcsFactoryStarted = new TaskCompletionSource();
    var tcsAllowFactoryToComplete = new TaskCompletionSource();

    // Task 1: Acquires lock
    var task1 = service.GetOrAddAsync("key1", async _ => {
      tcsFactoryStarted.SetResult();
      await tcsAllowFactoryToComplete.Task;
      return new MemoryStream();
    }, TestContext.Current.CancellationToken);

    await tcsFactoryStarted.Task;

    // Task 2: Evict (should be blocked)
    var evictTask = service.Evict(TestContext.Current.CancellationToken);

    await Task.Delay(50);
    Assert.False(evictTask.IsCompleted, "Evict should be blocked while Task 1 holds the lock.");

    // Release Task 1
    tcsAllowFactoryToComplete.SetResult();
    await task1;
    await evictTask;

    Assert.True(evictTask.IsCompletedSuccessfully);
  }

  // Helper to check if a key exists in the cache directly or by side-effect
  // since InMemoryCacheService doesn't expose Contains, we can check if GetOrAdd calls the factory.
  private async Task<bool> IsKeyInCache(InMemoryCacheService service, string key) {
    bool factoryCalled = false;
    await service.GetOrAddAsync(key, _ => {
      factoryCalled = true;
      return Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("re-added")));
    }, TestContext.Current.CancellationToken);
    return !factoryCalled;
  }
}
