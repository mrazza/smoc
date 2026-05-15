using Sharpcaster.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Smoc.Services.Cast;

public interface ICastDiscoveryService : IDisposable {
    event EventHandler<ChromecastReceiver>? DeviceFound;
    Task StartDiscoveryAsync();
    void StopDiscovery();
    IEnumerable<ChromecastReceiver> DiscoveredDevices { get; }
}