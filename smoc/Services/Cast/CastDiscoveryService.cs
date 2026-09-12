using Sharpcaster;
using Sharpcaster.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Smoc.Services.Cast;

/// <summary>
/// Service for discovery of Google Cast devices using SharpCaster.
/// </summary>
public sealed class CastDiscoveryService : ICastDiscoveryService {
  private readonly ChromecastLocator _locator;
  private readonly List<ChromecastReceiver> _discoveredDevices = new();

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
  public IEnumerable<ChromecastReceiver> DiscoveredDevices => _discoveredDevices.AsReadOnly();

  /// <inheritdoc/>
  public async Task StartDiscoveryAsync() {
    _discoveredDevices.Clear();
    var devices = await _locator.FindReceiversAsync(TimeSpan.FromSeconds(5));
    foreach (var device in devices) {
      AddDevice(device);
    }
  }

  private void OnReceiverFound(object? sender, ChromecastReceiverEventArgs e) {
    AddDevice(e.Receiver);
  }

  private void AddDevice(ChromecastReceiver device) {
    if (!_discoveredDevices.Any(d => d.DeviceUri == device.DeviceUri)) {
      _discoveredDevices.Add(device);
      DeviceFound?.Invoke(this, device);
    }
  }

  /// <inheritdoc/>
  public void StopDiscovery() {
  }

  /// <inheritdoc/>
  public void Dispose() {
    _locator.ChromecastReceiverFound -= OnReceiverFound;
  }
}
