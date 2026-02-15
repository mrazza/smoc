using System.Data.SqlTypes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using Terminal.Gui.App;

namespace Smoc.Services.Caching;

/// <summary>
/// A cache service that stores streams in temporary files.
/// </summary>
public class TempFileCacheService : ICacheService {
  /// <summary>
  /// The persistence characteristics of the cache.
  /// </summary>
  public enum CachePersistence {
    /// <summary>
    /// The cache may be deleted when the application is running, closes, or the system is rebooted.
    /// </summary>
    VOLATILE,
    /// <summary>
    /// The cache will be placed in a location designed to persist across application or system restarts.
    /// </summary>
    SEMIPERSISTENT
  }

  private readonly string _cacheDirectory;
  private readonly string _subDirectory;
  private readonly CacheConfig _cacheConfig;

  /// <summary>
  /// Creates a new instance of the <see cref="TempFileCacheService"/> class.
  /// </summary>
  /// <param name="subDirectory">The sub-directory to use for caching.</param>
  /// <param name="cacheConfig">The configuration for how this cache should behave.</param>
  /// <param name="cachePersistence">The persistence of the cache.</param>
  public TempFileCacheService(string subDirectory, CacheConfig cacheConfig, CachePersistence cachePersistence = CachePersistence.SEMIPERSISTENT) {
    _subDirectory = subDirectory;
    _cacheConfig = cacheConfig;
    _cacheDirectory = cachePersistence switch {
      CachePersistence.VOLATILE => GetVolitileCacheDirectory(subDirectory),
      CachePersistence.SEMIPERSISTENT => GetSemiPersistentCacheDirectory(subDirectory),
      _ => throw new NotImplementedException($"Cache persistence {cachePersistence} is not implemented.")
    };
  }

  /// <inheritdoc/>
  public async Task<Stream> GetOrAddAsync(string key, Func<CancellationToken, Task<Stream>> factory, CancellationToken cancellationToken = default) {
    // NOTE: We manually create file streams to ensure useAsync is set to true.
    // Without this, async methods tend to operate synchronously anyway.
    string filePath = Path.Combine(_cacheDirectory, key);
    if (await Task.Run(() => File.Exists(filePath), cancellationToken)) {
      Logging.Debug($"[Cache|{_subDirectory}] Found cached entity: {key}");
      await Task.Run(() => File.SetLastAccessTimeUtc(filePath, DateTime.UtcNow), cancellationToken);
      return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
    }
    Logging.Debug($"[Cache|{_subDirectory}] No cached entity found for key: {key}. Fetching from source.");
    var cachedEntityTask = factory(cancellationToken);
    var evictTask = CleanCacheDirectory(cancellationToken);
    await Task.WhenAll(cachedEntityTask, evictTask);
    var cachedEntity = cachedEntityTask.Result;
    using (var fileStream = new FileStream(filePath,
        FileMode.Create, FileAccess.Write, FileShare.None,
        bufferSize: 4096, useAsync: true)) {
      await cachedEntity.CopyToAsync(fileStream, cancellationToken);
    }
    cachedEntity.Seek(0, SeekOrigin.Begin);
    return cachedEntity;
  }

  /// <inheritdoc/>
  public async Task Evict(CancellationToken cancellationToken = default) => await CleanCacheDirectory(cancellationToken);

  private async Task CleanCacheDirectory(CancellationToken cancellationToken = default) {
    // If no max size, short circuit.
    if (_cacheConfig.MaxElements is null && _cacheConfig.MaxSizeBytes is null) return;

    await Task.Run(() => {
      var directoryInfo = new DirectoryInfo(_cacheDirectory);
      // Create a list of files ordered such that the 0-th index is the file we are
      // least likely to delete and the last index is the first file to go.
      List<FileInfo> files = [];
      switch (_cacheConfig.EvictionStrategy) {
        case EvictionStrategy.LRU:
          files = directoryInfo.GetFiles().OrderByDescending(file => file.LastAccessTimeUtc).ToList();
          break;
        case EvictionStrategy.LARGEST_FIRST:
          files = directoryInfo.GetFiles().OrderBy(file => file.Length).ToList();
          break;
        case EvictionStrategy.SMALLEST_FIRST:
          files = directoryInfo.GetFiles().OrderByDescending(file => file.Length).ToList();
          break;
        default:
          throw new NotImplementedException($"Eviction strategy {_cacheConfig.EvictionStrategy} is not implemented.");
      }

      int fileCount = files.Count;
      if (_cacheConfig.MaxElements is int maxFiles && files.Count > maxFiles) {
        var filesToDelete = files.Skip(maxFiles);
        foreach (var file in filesToDelete) {
          try {
            file.Delete();
          } catch (IOException e) {
            Logging.Warning($"Failed to delete file exceeding cache element limits: {file.FullName} cause {e.Message}");
          }
          cancellationToken.ThrowIfCancellationRequested();
        }
        fileCount = maxFiles;
      }

      if (_cacheConfig.MaxSizeBytes is long maxSizeBytes) {
        long totalSize = 0;
        foreach (var file in files.Take(fileCount)) {
          totalSize += file.Length;
          if (totalSize > maxSizeBytes) {
            try {
              file.Delete();
            } catch (IOException e) {
              Logging.Warning($"Failed to delete file exceeding cache size limits: {file.FullName} cause {e.Message}");
            }
          }
          cancellationToken.ThrowIfCancellationRequested();
        }
      }
    }, cancellationToken);
  }

  private static string GetVolitileCacheDirectory(string? subDirectory = null) {
    var baseCachePath = Path.Combine(Path.GetTempPath(), Program.ProductName.ToLowerInvariant(), "cache");
    if (!string.IsNullOrEmpty(subDirectory)) {
      baseCachePath = Path.Combine(baseCachePath, subDirectory);
    }

    if (!Directory.Exists(baseCachePath)) {
      Directory.CreateDirectory(baseCachePath);
    }

    return baseCachePath;
  }

  private static string GetSemiPersistentCacheDirectory(string? subDirectory = null) {
    string baseCachePath;

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
      // Windows: %LOCALAPPDATA%\SMoC\Cache
      baseCachePath = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
          Program.ProductName,
          "Cache"
      );
    } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
      // macOS: ~/Library/Caches/com.smoc.cache
      // Note: Apple prefers a reverse-DNS style name for the subfolder
      baseCachePath = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
          "Library",
          "Caches",
          $"com.{Program.ProductName.ToLower()}.cache"
      );
    } else {
      // Linux/Unix (XDG): ~/.cache/smoc
      // Check for XDG_CACHE_HOME first, otherwise default to ~/.cache
      string? xdgCache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");

      if (!string.IsNullOrEmpty(xdgCache)) {
        baseCachePath = Path.Combine(xdgCache, Program.ProductName.ToLowerInvariant());
      } else {
        baseCachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache",
            Program.ProductName.ToLowerInvariant()
        );
      }
    }

    if (!string.IsNullOrEmpty(subDirectory)) {
      baseCachePath = Path.Combine(baseCachePath, subDirectory);
    }

    if (!Directory.Exists(baseCachePath)) {
      Directory.CreateDirectory(baseCachePath);
    }

    return baseCachePath;
  }
}