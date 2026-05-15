using Sharpcaster.Models;
using Smoc.Services.Cast;
using Smoc.Streaming;
using Smoc.Services.Audio;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Smoc.Services.Audio.Cast;

public sealed class CastAudioService : IAudioService {
    private readonly ChromecastReceiver _device;
    private readonly IChromecastClient _client;
    private readonly IStreamingProxyService _proxyService;
    private float _volume = 0.5f;

    public CastAudioService(ChromecastReceiver device, IStreamingProxyService proxyService, IChromecastClient? client = null) {
        _device = device;
        _proxyService = proxyService;
        _client = client ?? new ChromecastClientWrapper();
    }

    public float Volume {
        get => _volume;
        set {
            _volume = value;
            _client.SetVolumeAsync(_volume).ConfigureAwait(false);
        }
    }

    public async Task ConnectAsync() {
        await _client.ConnectChromecast(_device);
        await _client.LaunchApplicationAsync("CC1AD845"); // Default Media Receiver
    }

    public IPlaybackService MakePlaybackService(Song song, Stream stream, string codec, CancellationToken cancellationToken = default) {
        var contentType = codec switch {
            "mp3" => "audio/mpeg",
            "flac" => "audio/flac",
            "m4a" => "audio/mp4",
            _ => "audio/mpeg"
        };
        
        var url = _proxyService.StartProxy(stream, contentType);
        return new CastPlaybackService(_client, song, stream, url, _proxyService);
    }

    public void Dispose() {
        _client.DisconnectAsync().ConfigureAwait(false);
        _client.Dispose();
    }
}