namespace Smoc.Services.Caching;

/// <summary>
/// An in-memory cache service.
/// </summary>
public class InMemoryCacheService : ICacheService {
  private readonly Dictionary<string, byte[]> _cache = new();

  /// <inheritdoc/>
  public async Task<Stream> GetOrAddAsync(string key, Func<CancellationToken, Task<Stream>> factory, CancellationToken cancellationToken = default) {
    if (_cache.TryGetValue(key, out var cachedEntity)) {
      return new MemoryStream(cachedEntity);
    }
    var result = await factory(cancellationToken);
    using (MemoryStream cachedStream = new((int)result.Length)) {
      await result.CopyToAsync(cachedStream, cancellationToken);
      _cache[key] = cachedStream.ToArray();
    }
    result.Seek(0, SeekOrigin.Begin);
    return result;
  }
}