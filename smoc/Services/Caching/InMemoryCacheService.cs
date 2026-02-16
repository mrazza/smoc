namespace Smoc.Services.Caching;

/// <summary>
/// An in-memory cache service.
/// </summary>
public class InMemoryCacheService : ICacheService {
  private class CacheValue(DateTime lastAccessedTimeUtc, byte[] data) {
    public DateTime LastAccessedTimeUtc { get; set; } = lastAccessedTimeUtc;
    public byte[] Data { get; init; } = data;
  }

  private readonly SemaphoreSlim _cacheLock;
  private readonly Dictionary<string, CacheValue> _cache;
  private readonly CacheConfig _cacheConfig;

  public InMemoryCacheService(CacheConfig cacheConfig) {
    _cacheConfig = cacheConfig;
    _cache = [];
    _cacheLock = new(1);
  }

  /// <inheritdoc/>
  public async Task<Stream> GetOrAddAsync(string key, Func<CancellationToken, Task<Stream>> factory, CancellationToken cancellationToken = default) {
    await _cacheLock.WaitAsync(cancellationToken);
    try {
      if (_cache.TryGetValue(key, out var cachedEntity)) {
        cachedEntity.LastAccessedTimeUtc = DateTime.UtcNow;
        return new MemoryStream(cachedEntity.Data);
      }
      var resultTask = factory(cancellationToken);
      var cleanTask = CleanDictionary(cancellationToken);
      await Task.WhenAll(resultTask, cleanTask);
      var result = resultTask.Result;
      using (MemoryStream cachedStream = new((int)result.Length)) {
        await result.CopyToAsync(cachedStream, cancellationToken);
        _cache[key] = new CacheValue(DateTime.UtcNow, cachedStream.ToArray());
      }
      result.Seek(0, SeekOrigin.Begin);
      return result;
    } finally {
      _cacheLock.Release();
    }
  }

  /// <inheritdoc/>
  public async Task Evict(CancellationToken cancellationToken = default) {
    await _cacheLock.WaitAsync(cancellationToken);
    try {
      await CleanDictionary(cancellationToken);
    } finally {
      _cacheLock.Release();
    }
  }

  /// <summary>
  /// Evicts items from the dictionary based on the CacheConfig.
  /// </summary>
  /// <remarks>
  /// This method must be executed within an exclusive lock on the underlying dictionary.
  /// </remarks>
  private async Task CleanDictionary(CancellationToken cancellationToken = default) {
    // If no max size, short circuit.
    if (_cacheConfig.MaxElements is null && _cacheConfig.MaxSizeBytes is null) return;

    await Task.Run(async () => {
      List<string> keys = [];
      // Create a list of files ordered such that the 0-th index is the file we are
      // least likely to delete and the last index is the first file to go.
      switch (_cacheConfig.EvictionStrategy) {
        case EvictionStrategy.LRU:
          keys = await _cache.ToAsyncEnumerable().OrderByDescending(kvp => kvp.Value.LastAccessedTimeUtc).Select(kvp => kvp.Key).ToListAsync(cancellationToken);
          break;
        case EvictionStrategy.LARGEST_FIRST:
          keys = await _cache.ToAsyncEnumerable().OrderBy(kvp => kvp.Value.Data.Length).Select(kvp => kvp.Key).ToListAsync(cancellationToken);
          break;
        case EvictionStrategy.SMALLEST_FIRST:
          keys = await _cache.ToAsyncEnumerable().OrderByDescending(kvp => kvp.Value.Data.Length).Select(kvp => kvp.Key).ToListAsync(cancellationToken);
          break;
        default:
          throw new NotImplementedException($"Eviction strategy {_cacheConfig.EvictionStrategy} is not implemented.");
      }
      cancellationToken.ThrowIfCancellationRequested();

      int entryCount = keys.Count;
      if (_cacheConfig.MaxElements is int maxElements && keys.Count > maxElements) {
        var keysToDelete = keys.Skip(maxElements);
        foreach (var key in keysToDelete) {
          _cache.Remove(key);
          cancellationToken.ThrowIfCancellationRequested();
        }
        entryCount = maxElements;
      }

      if (_cacheConfig.MaxSizeBytes is long maxSizeBytes) {
        long totalSize = 0;
        foreach (var key in keys.Take(entryCount)) {
          totalSize += _cache[key].Data.Length;
          if (totalSize > maxSizeBytes) {
            _cache.Remove(key);
          }
          cancellationToken.ThrowIfCancellationRequested();
        }
      }
    }, cancellationToken);
  }
}