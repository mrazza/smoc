using Sharpcaster;
using Sharpcaster.Models;
using Sharpcaster.Models.Media;
using System;
using System.Threading.Tasks;

namespace Smoc.Services.Cast;

public sealed class ChromecastClientWrapper : IChromecastClient {
    private readonly ChromecastClient _client = new();

    public event EventHandler<MediaStatus>? MediaStatusChanged {
        add => _client.MediaChannel.StatusChanged += value;
        remove => _client.MediaChannel.StatusChanged -= value;
    }

    public float Volume {
        get => (float)(_client.ChromecastStatus?.Volume?.Level ?? 0);
        set => _client.ReceiverChannel.SetVolume(value);
    }

    public async Task SetVolumeAsync(float level) {
        await _client.ReceiverChannel.SetVolume(level);
    }

    public async Task ConnectChromecast(ChromecastReceiver receiver) => await _client.ConnectChromecast(receiver);
    public async Task DisconnectAsync() => await _client.DisconnectAsync();
    public async Task LaunchApplicationAsync(string applicationId) => await _client.LaunchApplicationAsync(applicationId);
    
    public async Task LoadAsync(Media media) => await _client.MediaChannel.LoadAsync(media);
    public async Task PlayAsync() => await _client.MediaChannel.PlayAsync();
    public async Task PauseAsync() => await _client.MediaChannel.PauseAsync();
    public async Task StopAsync() => await _client.MediaChannel.StopAsync();
    public async Task SeekAsync(double seconds) => await _client.MediaChannel.SeekAsync(seconds);

    public void Dispose() => _client.Dispose();
}