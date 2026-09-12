using Sharpcaster.Models;
using Smoc.Services.Cast;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace smoc.Tests.Services.Cast;

/// <summary>
/// Unit tests for <see cref="CastDiscoveryService"/>.
/// </summary>
public class CastDiscoveryServiceTest : IDisposable {
  private readonly CastDiscoveryService _sut;

  public CastDiscoveryServiceTest() {
    _sut = new CastDiscoveryService();
  }

  [Fact]
  public void DiscoveredDevices_InitiallyEmpty() {
    Assert.Empty(_sut.DiscoveredDevices);
  }

  [Fact]
  public async Task EnsureInitialDiscoveryCompletedAsync_CompletesSafelyWhenNotStarted() {
    await _sut.EnsureInitialDiscoveryCompletedAsync(TestContext.Current.CancellationToken);
    Assert.Empty(_sut.DiscoveredDevices);
  }

  [Fact]
  public async Task StartDiscoveryAsync_InitiatesScan() {
    var task = _sut.StartDiscoveryAsync(TestContext.Current.CancellationToken);
    Assert.NotNull(task);
    await _sut.EnsureInitialDiscoveryCompletedAsync(TestContext.Current.CancellationToken);
  }

  [Fact]
  public async Task ScanAsync_ReturnsEnumerableOfDevices() {
    var devices = await _sut.ScanAsync(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
    Assert.NotNull(devices);
  }

  [Fact]
  public async Task StartDiscoveryAsync_WhenCanceled_InitiatesNewScan() {
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    var task1 = _sut.StartDiscoveryAsync(cts.Token);
    Assert.True(task1.IsCanceled);

    var task2 = _sut.StartDiscoveryAsync(TestContext.Current.CancellationToken);
    Assert.NotSame(task1, task2);
    await _sut.EnsureInitialDiscoveryCompletedAsync(TestContext.Current.CancellationToken);
  }

  public void Dispose() {
    _sut.Dispose();
  }
}
