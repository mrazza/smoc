using System.Runtime.InteropServices;
using Terminal.Gui.App;

namespace Smoc.Services.Caching;

/// <summary>
/// A cache service that stores streams in temporary files.
/// </summary>
public class TempFileCacheService : ICacheService {
  private readonly string _cacheDirectory;
  private readonly string _subDirectory;

  /// <summary>
  /// Creates a new instance of the <see cref="TempFileCacheService"/> class.
  /// </summary>
  /// <param name="subDirectory">The sub-directory to use for caching.</param>
  public TempFileCacheService(string subDirectory) {
    _subDirectory = subDirectory;
    _cacheDirectory = GetCacheDirectory(subDirectory);
  }

  /// <inheritdoc/>
  public async Task<Stream> GetOrAddAsync(string key, Func<CancellationToken, Task<Stream>> factory, CancellationToken cancellationToken = default) {
    string filePath = Path.Combine(_cacheDirectory, key);
    if (File.Exists(filePath)) {
      Logging.Debug($"[Cache|{_subDirectory}] Found cached entity: {key}");
      return File.OpenRead(filePath);
    }
    Logging.Debug($"[Cache|{_subDirectory}] No cached entity found for key: {key}. Fetching from source.");
    var cachedEntity = await factory(cancellationToken);
    using (var fileStream = File.OpenWrite(filePath)) {
      await cachedEntity.CopyToAsync(fileStream, cancellationToken);
    }
    cachedEntity.Seek(0, SeekOrigin.Begin);
    return cachedEntity;
  }

  private static string GetCacheDirectory(string? subDirectory = null) {
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
        baseCachePath = Path.Combine(xdgCache, Program.ProductName.ToLower());
      } else {
        baseCachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache",
            Program.ProductName.ToLower()
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