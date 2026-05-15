using Sharpcaster;
using Sharpcaster.Models;
using Sharpcaster.Models.Media;

namespace Smoc.Services.Cast;

public sealed class ChromecastClientWrapper : IChromecastClient {
    private readonly ChromecastClient _client = new();

    public event EventHandler<MediaStatus>? MediaStatusChanged {
        add => _client.MediaChannel.StatusChanged += value;
        remove => _client.MediaChannel.StatusChanged -= value;
    }

    public Task ConnectChromecast(ChromecastReceiver receiver) => _client.ConnectChromecast(receiver);
    public Task DisconnectAsync() => _client.DisconnectAsync();
    public Task LaunchApplicationAsync(string appId) => _client.LaunchApplicationAsync(appId);
    public void SetVolume(float volume) => _client.ReceiverChannel.SetVolume(volume);
    public Task LoadAsync(Media media) => _client.MediaChannel.LoadAsync(media);
    public Task PlayAsync() => _client.MediaChannel.PlayAsync();
    public Task PauseAsync() => _client.MediaChannel.PauseAsync();
    public Task StopAsync() => _client.MediaChannel.StopAsync();
    public Task SeekAsync(double seconds) => _client.MediaChannel.SeekAsync(seconds);

    public void Dispose() => _client.Dispose();
}