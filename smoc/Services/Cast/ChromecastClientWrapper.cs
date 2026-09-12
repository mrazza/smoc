using Sharpcaster;
using Sharpcaster.Models;
using Sharpcaster.Models.Media;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Smoc.Services.Cast;

/// <summary>
/// Wrapper for the SharpCaster <see cref="ChromecastClient"/>.
/// </summary>
public sealed class ChromecastClientWrapper : IChromecastClient {
  private readonly ChromecastClient _client = new();
  private bool _isConnected;

  /// <summary>
  /// Initializes a new instance of the <see cref="ChromecastClientWrapper"/> class.
  /// </summary>
  public ChromecastClientWrapper() {
    _client.Disconnected += (_, _) => _isConnected = false;
  }

  /// <inheritdoc/>
  public event EventHandler<MediaStatus>? MediaStatusChanged {
    add => _client.MediaChannel.StatusChanged += value;
    remove => _client.MediaChannel.StatusChanged -= value;
  }

  /// <inheritdoc/>
  public float Volume {
    get => (float)(_client.ChromecastStatus?.Volume?.Level ?? 0);
    set => _client.ReceiverChannel.SetVolume(value);
  }

  /// <inheritdoc/>
  public async Task SetVolumeAsync(float level, CancellationToken cancellationToken = default) {
    await _client.ReceiverChannel.SetVolume(level).WaitAsync(cancellationToken);
  }

  /// <inheritdoc/>
  public async Task EnsureConnectedAndLaunchedAsync(ChromecastReceiver receiver, string applicationId, CancellationToken cancellationToken = default) {
    if (!_isConnected) {
      await _client.ConnectChromecast(receiver).WaitAsync(cancellationToken);
      _isConnected = true;
    }

    try {
      var status = await _client.ReceiverChannel.GetChromecastStatusAsync().WaitAsync(cancellationToken);
      var isRunning = false;
      if (status?.Applications != null) {
        foreach (var app in status.Applications) {
          if (string.Equals(app.AppId, applicationId, StringComparison.OrdinalIgnoreCase)) {
            isRunning = true;
            break;
          }
        }
      }

      if (!isRunning) {
        await _client.LaunchApplicationAsync(applicationId).WaitAsync(cancellationToken);
      }
    } catch (OperationCanceledException) {
      throw;
    } catch {
      _isConnected = false;
      await _client.ConnectChromecast(receiver).WaitAsync(cancellationToken);
      _isConnected = true;
      await _client.LaunchApplicationAsync(applicationId).WaitAsync(cancellationToken);
    }
  }

  /// <inheritdoc/>
  public async Task ConnectChromecast(ChromecastReceiver receiver, CancellationToken cancellationToken = default) {
    await _client.ConnectChromecast(receiver).WaitAsync(cancellationToken);
    _isConnected = true;
  }

  /// <inheritdoc/>
  public async Task DisconnectAsync(CancellationToken cancellationToken = default) {
    _isConnected = false;
    await _client.DisconnectAsync().WaitAsync(cancellationToken);
  }

  /// <inheritdoc/>
  public Task LaunchApplicationAsync(string applicationId, CancellationToken cancellationToken = default) =>
    _client.LaunchApplicationAsync(applicationId).WaitAsync(cancellationToken);

  /// <inheritdoc/>
  public Task LoadAsync(Media media, CancellationToken cancellationToken = default) =>
    _client.MediaChannel.LoadAsync(media).WaitAsync(cancellationToken);

  /// <inheritdoc/>
  public Task PlayAsync(CancellationToken cancellationToken = default) =>
    _client.MediaChannel.PlayAsync().WaitAsync(cancellationToken);

  /// <inheritdoc/>
  public Task PauseAsync(CancellationToken cancellationToken = default) =>
    _client.MediaChannel.PauseAsync().WaitAsync(cancellationToken);

  /// <inheritdoc/>
  public Task StopAsync(CancellationToken cancellationToken = default) =>
    _client.MediaChannel.StopAsync().WaitAsync(cancellationToken);

  /// <inheritdoc/>
  public Task SeekAsync(double seconds, CancellationToken cancellationToken = default) =>
    _client.MediaChannel.SeekAsync(seconds).WaitAsync(cancellationToken);

  /// <inheritdoc/>
  public async Task<MediaStatus?> GetMediaStatusAsync(CancellationToken cancellationToken = default) {
    try {
      return await _client.MediaChannel.GetMediaStatusAsync().WaitAsync(cancellationToken);
    } catch {
      return null;
    }
  }

  /// <inheritdoc/>
  public void Dispose() => _client.Dispose();
}
