using SharpCaster.Models;
using SharpCaster.Services;
using Smoc.Services.Cast;
using Smoc.Streaming;

namespace Smoc.Services.Audio.Cast;

public sealed class CastAudioService : IAudioService {
    private readonly Chromecast _device;
    private readonly SharpCaster.ChromeCastClient _client;
    private readonly IStreamingProxyService _proxyService;
    private float _volume = 0.5f;

    public CastAudioService(Chromecast device, IStreamingProxyService proxyService) {
        _device = device;
        _proxyService = proxyService;
        _client = new SharpCaster.ChromeCastClient();
    }

    public float Volume {
        get => _volume;
        set {
            _volume = value;
            // We'll figure out volume later
        }
    }

    public async Task ConnectAsync() {
        // ChromeCastClient.ConnectChromecast takes a Uri in some versions, or a Chromecast object.
        // Based on strings, it might be ConnectChromecast(Chromecast device)
        // But the error said it couldn't convert from Chromecast to Uri.
        // So it wants a Uri. device.DeviceUri is a Uri.
        await _client.ConnectChromecast(_device.DeviceUri);
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
        _client.DisconnectChromecast();
    }
}