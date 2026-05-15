using SharpCaster.Models;
using SharpCaster.Services;

namespace Smoc.Services.Cast;

public sealed class CastDiscoveryService : ICastDiscoveryService {
    private readonly List<Chromecast> _discoveredDevices = new();

    public event EventHandler<Chromecast>? DeviceFound;

    public CastDiscoveryService() {
    }

    public IEnumerable<Chromecast> DiscoveredDevices => _discoveredDevices.AsReadOnly();

    public async Task StartDiscoveryAsync() {
        _discoveredDevices.Clear();
        try {
            var locator = new SharpCaster.DeviceLocator();
            var devices = await locator.LocateDevicesAsync();
            foreach (var device in devices) {
                if (!_discoveredDevices.Any(d => d.DeviceUri == device.DeviceUri)) {
                    _discoveredDevices.Add(device);
                    DeviceFound?.Invoke(this, device);
                }
            }
        } catch (Exception) {
            // Ignore discovery errors for now
        }
    }

    public void StopDiscovery() {
    }

    public void Dispose() {
    }
}