using System.Text;
using Smoc.Services.Caching;

namespace smoc.Tests.Services.Caching;

public class NoCachingCacheServiceTest {
  [Fact]
  public async Task GetOrAdd_ReturnsValue() {
    var cacheService = new NoCachingCacheService();
    using var stream = new MemoryStream(Encoding.ASCII.GetBytes("data"));
    var value = await cacheService.GetOrAddAsync("key", (_) => Task.FromResult<Stream>(stream), TestContext.Current.CancellationToken);
    Assert.Equal(stream, value);
  }

  [Fact]
  public async Task GetOrAdd_AlwaysReturnsNewValue() {
    var cacheService = new NoCachingCacheService();
    using var stream = new MemoryStream(Encoding.ASCII.GetBytes("data"));
    var value = await cacheService.GetOrAddAsync("key", (_) => Task.FromResult<Stream>(stream), TestContext.Current.CancellationToken);

    using var stream2 = new MemoryStream(Encoding.ASCII.GetBytes("data2"));
    value = await cacheService.GetOrAddAsync("key", (_) => Task.FromResult<Stream>(stream2), TestContext.Current.CancellationToken);
    Assert.Equal(stream2, value);
  }
  [Fact]
  public async Task Evict_CompletesSuccessfully() {
    var cacheService = new NoCachingCacheService();
    await cacheService.Evict(TestContext.Current.CancellationToken);
  }
}
