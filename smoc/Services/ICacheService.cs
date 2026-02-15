namespace Smoc.Services;

/// <summary>
/// A service for caching streams.
/// </summary>
public interface ICacheService {

  /// <summary>
  /// Gets a stream from the cache or creates a new one using the factory and caches it.
  /// </summary>
  /// <remarks>
  /// Callers take ownership of the returned stream and must dispose of it.
  /// </remarks>
  /// <param name="key">The key to use for caching.</param>
  /// <param name="factory">The factory to use to create a new stream if uncached.</param>
  /// <param name="cancellationToken">The cancellation token to use for the operation.</param>
  /// <returns>The stream.</returns>
  Task<Stream> GetOrAddAsync(string key, Func<CancellationToken, Task<Stream>> factory, CancellationToken cancellationToken = default);

  /// <summary>
  /// Evicts entities in the cache that exceed some configured thresholds.
  /// </summary>
  /// <param name="cancellationToken">The cancellation token to use for the operation.</param>
  Task Evict(CancellationToken cancellationToken = default);
}