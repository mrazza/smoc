using Sharpcaster.Models;
using Sharpcaster.Models.Media;
using System;
using System.Threading.Tasks;

namespace Smoc.Services.Cast;

public interface IChromecastClient : IDisposable {
    event EventHandler<MediaStatus>? MediaStatusChanged;
    Task ConnectChromecast(ChromecastReceiver receiver);
    Task DisconnectAsync();
    Task LaunchApplicationAsync(string applicationId);
    Task SetVolumeAsync(float level);
    Task LoadAsync(Media media);
    Task PlayAsync();
    Task PauseAsync();
    Task StopAsync();
    Task SeekAsync(double seconds);
    float Volume { get; set; }
}