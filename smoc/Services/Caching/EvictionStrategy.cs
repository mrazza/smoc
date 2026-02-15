namespace Smoc.Services.Caching;

/// <summary>
/// The eviction strategy to use when cleaning the cache.
/// </summary>
public enum EvictionStrategy {
  /// <summary>
  /// Least Recently Used
  /// </summary>
  LRU,
  /// <summary>
  /// Largest First (by size in bytes)
  /// </summary>
  LARGEST_FIRST,
  /// <summary>
  /// Smallest First (by size in bytes)
  /// </summary>
  SMALLEST_FIRST
}