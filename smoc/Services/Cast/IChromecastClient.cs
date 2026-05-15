using Sharpcaster.Models;
using Sharpcaster.Models.Media;

namespace Smoc.Services.Cast;

public interface IChromecastClient : IDisposable {
    event EventHandler<MediaStatus> MediaStatusChanged;
    Task ConnectChromecast(ChromecastReceiver receiver);
    Task DisconnectAsync();
    Task LaunchApplicationAsync(string appId);
    void SetVolume(float volume);
    Task LoadAsync(Media media);
    Task PlayAsync();
    Task PauseAsync();
    Task StopAsync();
    Task SeekAsync(double seconds);
}