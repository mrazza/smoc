namespace Smoc.Services.Caching;

/// <summary>
/// Configuration for the cache service. Defaults to infinite cache size.
/// </summary>
/// <param name="MaxElements">The maximum number of elements to store in the cache, or <c>null</c> for unlimited.</param>
/// <param name="MaxSizeBytes">The maximum total size in bytes of elements to store in the cache, or <c>null</c> for unlimited.</param>
/// <param name="EvictionStrategy">The eviction strategy to use when cache limits are exceeded.</param>
public sealed record CacheConfig(int? MaxElements = null, long? MaxSizeBytes = null, EvictionStrategy EvictionStrategy = EvictionStrategy.LRU);
