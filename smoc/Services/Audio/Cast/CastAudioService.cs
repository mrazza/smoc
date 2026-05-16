using Sharpcaster.Models;
using Smoc.Services.Cast;
using Smoc.Streaming;
using Smoc.Services.Audio;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Smoc.Services.Audio.Cast;

/// <summary>
/// Audio service for playing media on a Google Cast device.
/// </summary>
public sealed class CastAudioService : IAudioService {
    private readonly ChromecastReceiver _device;
    private readonly IChromecastClient _client;
    private readonly IStreamingProxyService _proxyService;
    private float _volume = 0.5f;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastAudioService"/> class.
    /// </summary>
    /// <param name="device">The Cast device to play on.</param>
    /// <param name="proxyService">The streaming proxy service.</param>
    /// <param name="client">An optional Cast client; if null, a default one will be created.</param>
    public CastAudioService(ChromecastReceiver device, IStreamingProxyService proxyService, IChromecastClient? client = null) {
        _device = device;
        _proxyService = proxyService;
        _client = client ?? new ChromecastClientWrapper();
    }

    /// <inheritdoc/>
    public float Volume {
        get => _volume;
        set {
            _volume = value;
            _client.SetVolumeAsync(_volume).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Connects to the Cast device.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ConnectAsync() {
        await _client.ConnectChromecast(_device);
        await _client.LaunchApplicationAsync("CC1AD845"); // Default Media Receiver
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public void Dispose() {
        _client.DisconnectAsync().ConfigureAwait(false);
        _client.Dispose();
    }
}