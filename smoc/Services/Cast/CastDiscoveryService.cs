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
        // The error said it wants TimeSpan?, not CancellationToken
        var devices = await _locator.FindReceiversAsync(TimeSpan.FromSeconds(5));
        foreach (var device in devices) {
            AddDevice(device);
        }
    }

    private void OnReceiverFound(object? sender, ChromecastReceiverEventArgs e) {
        // e.Receiver was my guess, let's try to verify if it has Receiver or if it IS the receiver
        // Based on "ChromecastReceiverEventArgs", it usually wraps the receiver.
        // Actually, let's check my previous 'strings' output for ChromecastReceiverEventArgs
        // It had get_Breaks, get_Tracks, etc. Wait.
        // Let's use a trick to see what it has if this fails, but usually it's e.Receiver or e.Chromecast.
        // Looking back at the README I scraped:
        // _locator.ChromecastReceiverFound += OnReceiverFound;
        // ...
        // private void OnReceiverFound(object sender, ChromecastReceiver e)
        // Wait, the README said ChromecastReceiver e. But the compiler error said ChromecastReceiverEventArgs.
        // Maybe the README is slightly outdated or for a different sub-version.
        // If it is ChromecastReceiverEventArgs, I'll try e.Receiver.
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