using Sharpcaster.Models;
using System;
using System.Collections.Generic;
using System.Threading;
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
  /// <param name="cancellationToken">A token to cancel the operation.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  Task StartDiscoveryAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Stops the discovery process.
  /// </summary>
  void StopDiscovery();

  /// <summary>
  /// Actively scans for Google Cast devices on the network.
  /// </summary>
  /// <param name="timeout">Optional scan timeout duration.</param>
  /// <param name="cancellationToken">A token to cancel the operation.</param>
  /// <returns>A task representing the asynchronous operation, returning newly discovered devices.</returns>
  Task<IEnumerable<ChromecastReceiver>> ScanAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);

  /// <summary>
  /// Ensures that the initial startup discovery scan has completed.
  /// </summary>
  /// <param name="cancellationToken">A token to cancel the operation.</param>
  /// <returns>A task representing the asynchronous operation.</returns>
  Task EnsureInitialDiscoveryCompletedAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Gets the list of currently discovered Google Cast devices.
  /// </summary>
  IEnumerable<ChromecastReceiver> DiscoveredDevices { get; }
}
