namespace Smoc.Services.Caching;

/// <summary>
/// Configuration for the cache service. Defaults to infinite cache size.
/// </summary>
public sealed record CacheConfig(int? MaxElements = null, long? MaxSizeBytes = null, EvictionStrategy EvictionStrategy = EvictionStrategy.LRU);