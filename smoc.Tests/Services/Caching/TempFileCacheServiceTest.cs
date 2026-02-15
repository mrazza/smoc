using System.Net.Http.Headers;
using System.Text;
using Smoc.Services.Caching;
using static Smoc.Services.Caching.TempFileCacheService;

namespace smoc.Tests.Services.Caching;

public class TempFileCacheServiceTest : IDisposable {
  private string? _testDirectory;
  private readonly string _subDirectory;

  public TempFileCacheServiceTest() {
    // Create a unique subdirectory for each test to ensure isolation
    _subDirectory = Guid.NewGuid().ToString();
  }

  public void Dispose() {
    Assert.NotNull(_testDirectory);
    if (Directory.Exists(_testDirectory)) {
      Directory.Delete(_testDirectory, true);
    }
  }

  private string InitDirectory(CachePersistence persistenceType) {
    // TODO: Add support for Windows and MacOS
    _testDirectory = persistenceType switch {
      // We know from the implementation that VOLATILE uses Path.GetTempPath()/SMoC/cache/subdir
      CachePersistence.VOLATILE => Path.Combine(Path.GetTempPath(), Smoc.Program.ProductName.ToLowerInvariant(), "cache", _subDirectory),
      // We know from the implementation that on Linux SEMIPERSISTENT uses Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)/.config/SMoC/cache/subdir
      CachePersistence.SEMIPERSISTENT => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", Smoc.Program.ProductName.ToLowerInvariant(), _subDirectory),
      _ => throw new ArgumentException($"Cache persistence type {persistenceType} is not supported.", nameof(persistenceType))
    };

    return _testDirectory;
  }

  [Theory]
  [CombinatorialData]
  public void Constructor_CreatesDirectory(CachePersistence cachePersistence) {
    InitDirectory(cachePersistence);
    var config = new CacheConfig();
    Assert.False(Directory.Exists(_testDirectory));
    _ = new TempFileCacheService(_subDirectory, config, cachePersistence);
    Assert.True(Directory.Exists(_testDirectory));
  }

  [Theory]
  [CombinatorialData]
  public async Task GetOrAddAsync_OnCacheMiss_CreatesFile(CachePersistence cachePersistence) {
    var testDirectory = InitDirectory(cachePersistence);
    var config = new CacheConfig();
    var service = new TempFileCacheService(_subDirectory, config, cachePersistence);
    string key = "test-key";
    string expectedContent = "hello world";

    using var resultStream = await service.GetOrAddAsync(key, async (ct) => {
      var stream = new MemoryStream(Encoding.UTF8.GetBytes(expectedContent));
      return stream;
    }, TestContext.Current.CancellationToken);

    Assert.NotNull(resultStream);
    using var reader = new StreamReader(resultStream);
    string content = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    Assert.Equal(expectedContent, content);

    string expectedFilePath = Path.Combine(testDirectory, key);
    Assert.True(File.Exists(expectedFilePath), $"Expected file not found: {expectedFilePath}");
  }

  [Theory]
  [CombinatorialData]
  public async Task GetOrAddAsync_OnCacheHit_ReturnsCachedFile(CachePersistence cachePersistence) {
    var testDirectory = InitDirectory(cachePersistence);
    var config = new CacheConfig();
    var service = new TempFileCacheService(_subDirectory, config, cachePersistence);
    string key = "test-key";
    string expectedContent = "cached content";
    bool factoryCalled = false;

    string filePath = Path.Combine(testDirectory, key);
    Directory.CreateDirectory(testDirectory);
    await File.WriteAllTextAsync(filePath, expectedContent, TestContext.Current.CancellationToken);
    using var resultStream = await service.GetOrAddAsync(key, (ct) => {
      factoryCalled = true;
      return Task.FromResult<Stream>(new MemoryStream());
    }, TestContext.Current.CancellationToken);

    Assert.False(factoryCalled, "Factory should not be called on cache hit");
    using var reader = new StreamReader(resultStream);
    string content = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    Assert.Equal(expectedContent, content);
  }

  [Theory]
  [CombinatorialData]
  public async Task Evict_LRU_RespectsMaxElements(CachePersistence cachePersistence) {
    var testDirectory = InitDirectory(cachePersistence);
    int maxElements = 2;
    var config = new CacheConfig(MaxElements: maxElements, EvictionStrategy: EvictionStrategy.LRU);
    var service = new TempFileCacheService(_subDirectory, config, cachePersistence);

    string file1 = Path.Combine(testDirectory, "file1");
    string file2 = Path.Combine(testDirectory, "file2");
    string file3 = Path.Combine(testDirectory, "file3");
    Directory.CreateDirectory(testDirectory);

    await File.WriteAllTextAsync(file1, "content1", TestContext.Current.CancellationToken);
    await File.WriteAllTextAsync(file2, "content2", TestContext.Current.CancellationToken);
    await File.WriteAllTextAsync(file3, "content3", TestContext.Current.CancellationToken);

    File.SetLastAccessTimeUtc(file1, DateTime.UtcNow.AddMinutes(-30));
    File.SetLastAccessTimeUtc(file2, DateTime.UtcNow.AddMinutes(-20));
    File.SetLastAccessTimeUtc(file3, DateTime.UtcNow.AddMinutes(-10));

    await service.Evict(TestContext.Current.CancellationToken);

    Assert.False(File.Exists(file1), "Oldest file should be evicted");
    Assert.True(File.Exists(file2), "Newer file should be kept");
    Assert.True(File.Exists(file3), "Newest file should be kept");
  }

  [Theory]
  [CombinatorialData]
  public async Task Evict_LargestFirst_RespectsMaxElements(CachePersistence cachePersistence) {
    var testDirectory = InitDirectory(cachePersistence);
    int maxElements = 2;
    var config = new CacheConfig(MaxElements: maxElements, EvictionStrategy: EvictionStrategy.LARGEST_FIRST);
    var service = new TempFileCacheService(_subDirectory, config, cachePersistence);

    string smallFile = Path.Combine(testDirectory, "small");
    string mediumFile = Path.Combine(testDirectory, "medium");
    string largeFile = Path.Combine(testDirectory, "large");
    Directory.CreateDirectory(testDirectory);

    await File.WriteAllTextAsync(smallFile, "s", TestContext.Current.CancellationToken);
    await File.WriteAllTextAsync(mediumFile, "mm", TestContext.Current.CancellationToken);
    await File.WriteAllTextAsync(largeFile, "lll", TestContext.Current.CancellationToken);

    await service.Evict(TestContext.Current.CancellationToken);

    Assert.False(File.Exists(largeFile), "Largest file should be evicted");
    Assert.True(File.Exists(mediumFile), "Medium file should be kept");
    Assert.True(File.Exists(smallFile), "Small file should be kept");
  }

  [Theory]
  [CombinatorialData]
  public async Task Evict_SmallestFirst_RespectsMaxElements(CachePersistence cachePersistence) {
    var testDirectory = InitDirectory(cachePersistence);
    int maxElements = 2;
    var config = new CacheConfig(MaxElements: maxElements, EvictionStrategy: EvictionStrategy.SMALLEST_FIRST);
    var service = new TempFileCacheService(_subDirectory, config, cachePersistence);

    string smallFile = Path.Combine(testDirectory, "small");
    string mediumFile = Path.Combine(testDirectory, "medium");
    string largeFile = Path.Combine(testDirectory, "large");
    Directory.CreateDirectory(testDirectory);

    await File.WriteAllTextAsync(smallFile, "s", TestContext.Current.CancellationToken);
    await File.WriteAllTextAsync(mediumFile, "mm", TestContext.Current.CancellationToken);
    await File.WriteAllTextAsync(largeFile, "lll", TestContext.Current.CancellationToken);

    await service.Evict(TestContext.Current.CancellationToken);

    Assert.False(File.Exists(smallFile), "Smallest file should be evicted");
    Assert.True(File.Exists(mediumFile), "Medium file should be kept");
    Assert.True(File.Exists(largeFile), "Large file should be kept");
  }

  [Theory]
  [CombinatorialData]
  public async Task Evict_LRU_RespectsMaxSizeBytes(CachePersistence cachePersistence) {
    var testDirectory = InitDirectory(cachePersistence);
    long maxSizeBytes = 2;
    var config = new CacheConfig(MaxSizeBytes: maxSizeBytes, EvictionStrategy: EvictionStrategy.LRU);
    var service = new TempFileCacheService(_subDirectory, config, cachePersistence);

    string file1 = Path.Combine(testDirectory, "file1");
    string file2 = Path.Combine(testDirectory, "file2");
    string file3 = Path.Combine(testDirectory, "file3");
    Directory.CreateDirectory(testDirectory);

    await File.WriteAllTextAsync(file1, "a", TestContext.Current.CancellationToken);
    await File.WriteAllTextAsync(file2, "b", TestContext.Current.CancellationToken);
    await File.WriteAllTextAsync(file3, "c", TestContext.Current.CancellationToken);

    File.SetLastAccessTimeUtc(file1, DateTime.UtcNow.AddMinutes(-10));
    File.SetLastAccessTimeUtc(file2, DateTime.UtcNow.AddMinutes(-20));
    File.SetLastAccessTimeUtc(file3, DateTime.UtcNow.AddMinutes(-30));

    await service.Evict(TestContext.Current.CancellationToken);

    Assert.True(File.Exists(file1), "Newest file should be kept");
    Assert.True(File.Exists(file2), "Second file fits in limit (1+1 <= 2)");
    Assert.False(File.Exists(file3), "Oldest file should be evicted because 1+1+1 > 2");
  }

  [Theory]
  [CombinatorialData]
  public async Task Evict_NoLimits_DoesNothing(CachePersistence cachePersistence) {
    var testDirectory = InitDirectory(cachePersistence);
    var config = new CacheConfig(MaxElements: null, MaxSizeBytes: null);
    var service = new TempFileCacheService(_subDirectory, config, cachePersistence);

    string file1 = Path.Combine(testDirectory, "file1");
    Directory.CreateDirectory(testDirectory);
    await File.WriteAllTextAsync(file1, "content", TestContext.Current.CancellationToken);

    await service.Evict(TestContext.Current.CancellationToken);

    Assert.True(File.Exists(file1));
  }
}
