using Sharpcaster;
using Sharpcaster.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Smoc.Services.Cast;

/// <summary>
/// Service for discovery of Google Cast devices using SharpCaster.
/// </summary>
public sealed class CastDiscoveryService : ICastDiscoveryService {
  private readonly ChromecastLocator _locator;
  private readonly List<ChromecastReceiver> _discoveredDevices = new();
  private readonly object _devicesLock = new();
  private readonly CancellationTokenSource _disposeCts = new();
  private Task? _initialDiscoveryTask;

  /// <inheritdoc/>
  public event EventHandler<ChromecastReceiver>? DeviceFound;

  /// <summary>
  /// Initializes a new instance of the <see cref="CastDiscoveryService"/> class.
  /// </summary>
  public CastDiscoveryService() {
    _locator = new ChromecastLocator();
    _locator.ChromecastReceiverFound += OnReceiverFound;
  }

  /// <inheritdoc/>
  public IEnumerable<ChromecastReceiver> DiscoveredDevices {
    get {
      lock (_devicesLock) {
        return _discoveredDevices.ToList();
      }
    }
  }

  /// <inheritdoc/>
  public Task StartDiscoveryAsync(CancellationToken cancellationToken = default) {
    lock (_devicesLock) {
      if (_initialDiscoveryTask == null || _initialDiscoveryTask.IsFaulted || _initialDiscoveryTask.IsCanceled) {
        _initialDiscoveryTask = ScanInternalAsync(
          quickTimeout: TimeSpan.FromMilliseconds(500),
          mediumTimeout: TimeSpan.FromSeconds(1),
          fullTimeout: TimeSpan.FromSeconds(2),
          cancellationToken: cancellationToken);
      }
      return _initialDiscoveryTask;
    }
  }

  /// <inheritdoc/>
  public async Task EnsureInitialDiscoveryCompletedAsync(CancellationToken cancellationToken = default) {
    Task? task;
    lock (_devicesLock) {
      task = _initialDiscoveryTask;
    }

    if (task != null) {
      try {
        await task.WaitAsync(cancellationToken);
      } catch (OperationCanceledException) {
        throw;
      } catch {
        // Suppress scan exceptions to allow subsequent fallback logic
      }
    }
  }

  /// <inheritdoc/>
  public async Task<IEnumerable<ChromecastReceiver>> ScanAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default) {
    var scanTimeout = timeout ?? TimeSpan.FromSeconds(2);
    return await ScanInternalAsync(
      quickTimeout: TimeSpan.FromMilliseconds(500),
      mediumTimeout: scanTimeout,
      fullTimeout: scanTimeout,
      cancellationToken: cancellationToken);
  }

  private async Task<IEnumerable<ChromecastReceiver>> ScanInternalAsync(
    TimeSpan quickTimeout,
    TimeSpan mediumTimeout,
    TimeSpan fullTimeout,
    CancellationToken cancellationToken = default) {
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, cancellationToken);
    var receivers = await _locator.FindReceiversAsync(quickTimeout, mediumTimeout, fullTimeout).WaitAsync(linkedCts.Token);
    var newlyDiscovered = new List<ChromecastReceiver>();
    foreach (var device in receivers) {
      if (AddDevice(device)) {
        newlyDiscovered.Add(device);
      }
    }
    return newlyDiscovered;
  }

  private void OnReceiverFound(object? sender, ChromecastReceiverEventArgs e) {
    AddDevice(e.Receiver);
  }

  private bool AddDevice(ChromecastReceiver device) {
    ChromecastReceiver? toNotify = null;
    lock (_devicesLock) {
      if (!_discoveredDevices.Any(d => d.DeviceUri == device.DeviceUri)) {
        _discoveredDevices.Add(device);
        toNotify = device;
      }
    }

    if (toNotify != null) {
      DeviceFound?.Invoke(this, toNotify);
      return true;
    }
    return false;
  }

  /// <inheritdoc/>
  public void StopDiscovery() {
  }

  /// <inheritdoc/>
  public void Dispose() {
    _locator.ChromecastReceiverFound -= OnReceiverFound;
    _disposeCts.Cancel();
    _disposeCts.Dispose();
  }
}
