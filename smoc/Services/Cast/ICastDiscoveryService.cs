using Sharpcaster.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Smoc.Services.Cast;

/// <summary>
/// Interface for discovery of Google Cast devices.
/// </summary>
public interface ICastDiscoveryService : IDisposable {
    /// <summary>
    /// Occurs when a new Google Cast device is discovered.
    /// </summary>
    event EventHandler<ChromecastReceiver>? DeviceFound;

    /// <summary>
    /// Starts the discovery process.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartDiscoveryAsync();

    /// <summary>
    /// Stops the discovery process.
    /// </summary>
    void StopDiscovery();

    /// <summary>
    /// Gets the list of currently discovered Google Cast devices.
    /// </summary>
    IEnumerable<ChromecastReceiver> DiscoveredDevices { get; }
}