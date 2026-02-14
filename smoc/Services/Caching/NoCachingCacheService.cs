namespace Smoc.Services.Caching;

/// <summary>
/// A cache service that does not cache anything.
/// </summary>
public class NoCachingCacheService : ICacheService {
  /// <inheritdoc/>
  public async Task<Stream> GetOrAddAsync(string key, Func<CancellationToken, Task<Stream>> factory, CancellationToken cancellationToken = default) {
    return await factory(cancellationToken);
  }
}