using Sharpcaster;
using Sharpcaster.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Smoc.Services.Cast;

public sealed class CastDiscoveryService : ICastDiscoveryService {
    private readonly ChromecastLocator _locator;
    private readonly List<ChromecastReceiver> _discoveredDevices = new();

    public event EventHandler<ChromecastReceiver>? DeviceFound;

    public CastDiscoveryService() {
        _locator = new ChromecastLocator();
        _locator.ChromecastReceiverFound += OnReceiverFound;
    }

    public IEnumerable<ChromecastReceiver> DiscoveredDevices => _discoveredDevices.AsReadOnly();

    public async Task StartDiscoveryAsync() {
        _discoveredDevices.Clear();
        var devices = await _locator.FindReceiversAsync();
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

    public void StopDiscovery() {
    }

    public void Dispose() {
        _locator.ChromecastReceiverFound -= OnReceiverFound;
    }
}