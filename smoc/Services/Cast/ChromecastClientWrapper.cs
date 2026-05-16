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
    public Task ConnectChromecast(ChromecastReceiver receiver) => _client.ConnectChromecast(receiver);

    /// <inheritdoc/>
    public Task DisconnectAsync() => _client.DisconnectAsync();

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
    public void Dispose() => _client.Dispose();
}