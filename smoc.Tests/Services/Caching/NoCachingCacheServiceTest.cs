using System.Text;
using Smoc.Services.Caching;

namespace smoc.Tests.Services.Caching;

public class NoCachingCacheServiceTest {
  [Fact]
  public async Task GetOrAdd_ReturnsValue() {
    var cacheService = new NoCachingCacheService();
    using var stream = new MemoryStream(Encoding.ASCII.GetBytes("data"));
    var value = await cacheService.GetOrAddAsync("key", (_) => Task.FromResult<Stream>(stream));
    Assert.Equal(stream, value);
  }

  [Fact]
  public async Task GetOrAdd_AlwaysReturnsNewValue() {
    var cacheService = new NoCachingCacheService();
    using var stream = new MemoryStream(Encoding.ASCII.GetBytes("data"));
    var value = await cacheService.GetOrAddAsync("key", (_) => Task.FromResult<Stream>(stream));

    using var stream2 = new MemoryStream(Encoding.ASCII.GetBytes("data2"));
    value = await cacheService.GetOrAddAsync("key", (_) => Task.FromResult<Stream>(stream2));
    Assert.Equal(stream2, value);
  }
}
