using Sharpcaster;
using Sharpcaster.Models;
using Sharpcaster.Models.Media;
using System;
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
  public async Task SetVolumeAsync(float level) {
    await _client.ReceiverChannel.SetVolume(level);
  }

  /// <inheritdoc/>
  public async Task EnsureConnectedAndLaunchedAsync(ChromecastReceiver receiver, string applicationId) {
    if (!_isConnected) {
      await _client.ConnectChromecast(receiver);
      _isConnected = true;
    }

    try {
      var status = await _client.ReceiverChannel.GetChromecastStatusAsync();
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
        await _client.LaunchApplicationAsync(applicationId);
      }
    } catch {
      _isConnected = false;
      await _client.ConnectChromecast(receiver);
      _isConnected = true;
      await _client.LaunchApplicationAsync(applicationId);
    }
  }

  /// <inheritdoc/>
  public async Task ConnectChromecast(ChromecastReceiver receiver) {
    await _client.ConnectChromecast(receiver);
    _isConnected = true;
  }

  /// <inheritdoc/>
  public async Task DisconnectAsync() {
    _isConnected = false;
    await _client.DisconnectAsync();
  }

  /// <inheritdoc/>
  public Task LaunchApplicationAsync(string applicationId) => _client.LaunchApplicationAsync(applicationId);

  /// <inheritdoc/>
  public Task LoadAsync(Media media) => _client.MediaChannel.LoadAsync(media);

  /// <inheritdoc/>
  public Task PlayAsync() => _client.MediaChannel.PlayAsync();

  /// <inheritdoc/>
  public Task PauseAsync() => _client.MediaChannel.PauseAsync();

  /// <inheritdoc/>
  public Task StopAsync() => _client.MediaChannel.StopAsync();

  /// <inheritdoc/>
  public Task SeekAsync(double seconds) => _client.MediaChannel.SeekAsync(seconds);

  /// <inheritdoc/>
  public async Task<MediaStatus?> GetMediaStatusAsync() {
    try {
      return await _client.MediaChannel.GetMediaStatusAsync();
    } catch {
      return null;
    }
  }

  /// <inheritdoc/>
  public void Dispose() => _client.Dispose();
}
