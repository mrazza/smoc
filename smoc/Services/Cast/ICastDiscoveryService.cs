using SharpCaster.Models;

namespace Smoc.Services.Cast;

public interface ICastDiscoveryService : IDisposable {
    event EventHandler<Chromecast> DeviceFound;
    Task StartDiscoveryAsync();
    void StopDiscovery();
    IEnumerable<Chromecast> DiscoveredDevices { get; }
}