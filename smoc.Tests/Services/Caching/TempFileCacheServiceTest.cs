using System.Net.Http.Headers;
using System.Text;
using Smoc.Services.Caching;
using static Smoc.Services.Caching.TempFileCacheService;

namespace smoc.Tests.Services.Caching;

/// <summary>
/// Unit tests for <see cref="TempFileCacheService"/>.
/// </summary>
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

  /// <summary>
  /// Verifies that on a cache miss, the file is created on disk and returned as a FileStream.
  /// </summary>
  [Theory]
  [CombinatorialData]
  public async Task GetOrAddAsync_OnCacheMiss_CreatesFile(CachePersistence cachePersistence) {
    var testDirectory = InitDirectory(cachePersistence);
    var config = new CacheConfig();
    var service = new TempFileCacheService(_subDirectory, config, cachePersistence);
    string key = "test-key";
    string expectedContent = "hello world";

    using var resultStream = await service.GetOrAddAsync(
      key,
      _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(expectedContent))),
      TestContext.Current.CancellationToken);

    Assert.NotNull(resultStream);
    var fileStream = Assert.IsType<FileStream>(resultStream);
    string expectedFilePath = Path.Combine(testDirectory, key);
    Assert.Equal(Path.GetFullPath(expectedFilePath), Path.GetFullPath(fileStream.Name));

    using var reader = new StreamReader(resultStream);
    string content = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    Assert.Equal(expectedContent, content);

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

  /// <summary>
  /// A stream implementation that does not support seeking, for testing purposes.
  /// </summary>
  private sealed class NonSeekableStream : Stream {
    private readonly MemoryStream _inner;

    /// <summary>
    /// Gets whether the stream has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NonSeekableStream"/> class.
    /// </summary>
    /// <param name="data">The byte data to expose in the stream.</param>
    public NonSeekableStream(byte[] data) {
      _inner = new MemoryStream(data);
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => _inner.Length;

    /// <inheritdoc/>
    public override long Position {
      get => throw new NotSupportedException();
      set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush() => _inner.Flush();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) {
      if (disposing) {
        IsDisposed = true;
        _inner.Dispose();
      }
      base.Dispose(disposing);
    }
  }

  /// <summary>
  /// A seekable stream that tracks whether it has been disposed.
  /// </summary>
  private sealed class TrackedSeekableStream : MemoryStream {
    /// <summary>
    /// Gets whether the stream has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackedSeekableStream"/> class.
    /// </summary>
    /// <param name="buffer">The underlying buffer.</param>
    public TrackedSeekableStream(byte[] buffer) : base(buffer) {
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) {
      if (disposing) {
        IsDisposed = true;
      }
      base.Dispose(disposing);
    }
  }

  /// <summary>
  /// Verifies that GetOrAddAsync succeeds with a non-seekable source stream, disposes it, and returns a seekable FileStream.
  /// </summary>
  [Theory]
  [CombinatorialData]
  public async Task GetOrAddAsync_WithNonSeekableStream_Succeeds(CachePersistence cachePersistence) {
    var testDirectory = InitDirectory(cachePersistence);
    var config = new CacheConfig();
    var service = new TempFileCacheService(_subDirectory, config, cachePersistence);
    string key = "non-seekable-key";
    string expectedContent = "non-seekable stream content";
    NonSeekableStream? sourceStream = null;

    using var resultStream = await service.GetOrAddAsync(
      key,
      _ => {
        sourceStream = new NonSeekableStream(Encoding.UTF8.GetBytes(expectedContent));
        return Task.FromResult<Stream>(sourceStream);
      },
      TestContext.Current.CancellationToken);

    Assert.NotNull(sourceStream);
    Assert.True(sourceStream.IsDisposed, "Source non-seekable stream must be disposed.");
    Assert.NotNull(resultStream);
    Assert.True(resultStream.CanSeek, "Returned stream must be seekable");
    var fileStream = Assert.IsType<FileStream>(resultStream);
    string expectedFilePath = Path.Combine(testDirectory, key);
    Assert.Equal(Path.GetFullPath(expectedFilePath), Path.GetFullPath(fileStream.Name));

    using var reader = new StreamReader(resultStream);
    string content = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    Assert.Equal(expectedContent, content);

    Assert.True(File.Exists(expectedFilePath), $"Expected file not found: {expectedFilePath}");
  }

  /// <summary>
  /// Verifies that on a cache miss, GetOrAddAsync always disposes the seekable source stream returned by
  /// the factory and returns a FileStream backed by the cached file on disk.
  /// </summary>
  [Theory]
  [CombinatorialData]
  public async Task GetOrAddAsync_OnCacheMiss_DisposesSeekableSourceStreamAndReturnsFileStream(CachePersistence cachePersistence) {
    var testDirectory = InitDirectory(cachePersistence);
    var config = new CacheConfig();
    var service = new TempFileCacheService(_subDirectory, config, cachePersistence);
    string key = "seekable-key";
    string expectedContent = "seekable content to cache";
    TrackedSeekableStream? sourceStream = null;
    bool wasSeekableBeforeDispose = false;

    using var resultStream = await service.GetOrAddAsync(
      key,
      _ => {
        sourceStream = new TrackedSeekableStream(Encoding.UTF8.GetBytes(expectedContent));
        wasSeekableBeforeDispose = sourceStream.CanSeek;
        return Task.FromResult<Stream>(sourceStream);
      },
      TestContext.Current.CancellationToken);

    Assert.NotNull(sourceStream);
    Assert.True(wasSeekableBeforeDispose, "Source stream must report CanSeek == true before disposal.");
    Assert.True(sourceStream.IsDisposed, "Source seekable stream must be disposed on cache miss.");

    Assert.NotNull(resultStream);
    var fileStream = Assert.IsType<FileStream>(resultStream);
    string expectedFilePath = Path.Combine(testDirectory, key);
    Assert.Equal(Path.GetFullPath(expectedFilePath), Path.GetFullPath(fileStream.Name));

    using var reader = new StreamReader(resultStream);
    string content = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    Assert.Equal(expectedContent, content);

    Assert.True(File.Exists(expectedFilePath), $"Expected file not found: {expectedFilePath}");
  }

  [Theory]
  [CombinatorialData]
  public async Task GetOrAddAsync_SanitizesInvalidKeyCharacters(CachePersistence cachePersistence) {
    var testDirectory = InitDirectory(cachePersistence);
    var config = new CacheConfig();
    var service = new TempFileCacheService(_subDirectory, config, cachePersistence);
    string key = "invalid/key:name?test";
    string expectedContent = "sanitized content";

    using var resultStream = await service.GetOrAddAsync(
      key,
      _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(expectedContent))),
      TestContext.Current.CancellationToken);

    Assert.NotNull(resultStream);
    using var reader = new StreamReader(resultStream);
    string content = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    Assert.Equal(expectedContent, content);

    // Verify sanitized file exists in directory
    var files = Directory.GetFiles(testDirectory);
    Assert.Single(files);
    Assert.False(Path.GetFileName(files[0]).Contains('/'));
    Assert.False(Path.GetFileName(files[0]).Contains(':'));
    Assert.False(Path.GetFileName(files[0]).Contains('?'));
  }

  [Theory]
  [CombinatorialData]
  public async Task Evict_LRU_RespectsMaxSizeBytes_DoesNotAccumulateEvictedSizes(CachePersistence cachePersistence) {
    var testDirectory = InitDirectory(cachePersistence);
    long maxSizeBytes = 3;
    var config = new CacheConfig(MaxSizeBytes: maxSizeBytes, EvictionStrategy: EvictionStrategy.LRU);
    var service = new TempFileCacheService(_subDirectory, config, cachePersistence);

    string file1 = Path.Combine(testDirectory, "f1");
    string file2 = Path.Combine(testDirectory, "f2");
    string file3 = Path.Combine(testDirectory, "f3");
    Directory.CreateDirectory(testDirectory);

    // f1 (oldest): 1 byte
    // f2 (middle): 5 bytes (oversized)
    // f3 (newest): 2 bytes
    await File.WriteAllTextAsync(file1, "a", TestContext.Current.CancellationToken);
    await File.WriteAllTextAsync(file2, "bbbbb", TestContext.Current.CancellationToken);
    await File.WriteAllTextAsync(file3, "cc", TestContext.Current.CancellationToken);

    File.SetLastAccessTimeUtc(file1, DateTime.UtcNow.AddMinutes(-30));
    File.SetLastAccessTimeUtc(file2, DateTime.UtcNow.AddMinutes(-20));
    File.SetLastAccessTimeUtc(file3, DateTime.UtcNow.AddMinutes(-10));

    await service.Evict(TestContext.Current.CancellationToken);

    Assert.True(File.Exists(file3), "Newest file should be kept");
    Assert.False(File.Exists(file2), "Oversized file should be evicted");
    Assert.True(File.Exists(file1), "Oldest file should be kept because f3 (2) + f1 (1) = 3 <= 3");
  }

  [Theory]
  [CombinatorialData]
  public async Task Evict_IgnoresTmpFiles(CachePersistence cachePersistence) {
    var testDirectory = InitDirectory(cachePersistence);
    var config = new CacheConfig(MaxElements: 1, EvictionStrategy: EvictionStrategy.LRU);
    var service = new TempFileCacheService(_subDirectory, config, cachePersistence);

    string normalFile = Path.Combine(testDirectory, "valid-file");
    string tmpFile = Path.Combine(testDirectory, "active-download.tmp");
    Directory.CreateDirectory(testDirectory);

    await File.WriteAllTextAsync(normalFile, "valid content", TestContext.Current.CancellationToken);
    await File.WriteAllTextAsync(tmpFile, "downloading content", TestContext.Current.CancellationToken);

    await service.Evict(TestContext.Current.CancellationToken);

    Assert.True(File.Exists(normalFile), "Normal file within limit should be kept");
    Assert.True(File.Exists(tmpFile), ".tmp file should not be counted or evicted");
  }

  /// <summary>
  /// Verifies that keys longer than 255 characters succeed and produce a bounded filename.
  /// </summary>
  [Theory]
  [CombinatorialData]
  public async Task GetOrAddAsync_WithKeyLongerThan255Chars_SucceedsAndBoundsFilename(CachePersistence cachePersistence) {
    var testDirectory = InitDirectory(cachePersistence);
    var config = new CacheConfig();
    var service = new TempFileCacheService(_subDirectory, config, cachePersistence);
    string key = new('a', 300);
    string expectedContent = "bounded filename content";

    using var resultStream = await service.GetOrAddAsync(
      key,
      _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(expectedContent))),
      TestContext.Current.CancellationToken);

    Assert.NotNull(resultStream);
    using var reader = new StreamReader(resultStream);
    string content = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    Assert.Equal(expectedContent, content);

    var files = Directory.GetFiles(testDirectory);
    Assert.Single(files);
    var fileName = Path.GetFileName(files[0]);
    Assert.True(fileName.Length <= 129);
    Assert.StartsWith(new string('a', 64) + "_", fileName);
  }
}
