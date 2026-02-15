namespace Smoc.Services.Caching;

/// <summary>
/// The eviction strategy to use when cleaning the cache.
/// </summary>
public enum EvictionStrategy {
  /// <summary>
  /// Least Recently Used
  /// </summary>
  LRU = 1,
  /// <summary>
  /// Largest First (by size in bytes)
  /// </summary>
  LARGEST_FIRST = 2,
  /// <summary>
  /// Smallest First (by size in bytes)
  /// </summary>
  SMALLEST_FIRST = 3
}