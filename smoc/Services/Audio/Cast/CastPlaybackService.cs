using Sharpcaster.Models.Media;
using Smoc.Services.Cast;
using Smoc.Streaming;
using Smoc.Services.Audio;
using System;
using System.IO;

namespace Smoc.Services.Audio.Cast;

/// <summary>
/// Playback service for a single song on a Google Cast device.
/// </summary>
public sealed class CastPlaybackService : IPlaybackService {
    private readonly IChromecastClient _client;
    private readonly Song _song;
    private readonly Stream _stream;
    private readonly string _url;
    private readonly IStreamingProxyService _proxyService;
    private PlaybackState _state = PlaybackState.Stopped;
    private TimeSpan _currentTime = TimeSpan.Zero;
    private TimeSpan _duration = TimeSpan.Zero;

    /// <inheritdoc/>
    public event EventHandler? SongEnded;

    /// <inheritdoc/>
    public event EventHandler<TimeSpan>? PositionChanged;

    /// <inheritdoc/>
    public event EventHandler<PlaybackState>? PlaybackStateChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="CastPlaybackService"/> class.
    /// </summary>
    /// <param name="client">The Cast client.</param>
    /// <param name="song">The song to play.</param>
    /// <param name="stream">The stream of the song.</param>
    /// <param name="url">The URL where the stream is proxied.</param>
    /// <param name="proxyService">The proxy service.</param>
    public CastPlaybackService(IChromecastClient client, Song song, Stream stream, string url, IStreamingProxyService proxyService) {
        _client = client;
        _song = song;
        _stream = stream;
        _url = url;
        _proxyService = proxyService;

        _client.MediaStatusChanged += OnMediaStatusChanged;
    }

    /// <inheritdoc/>
    public TimeSpan CurrentTime => _currentTime;

    /// <inheritdoc/>
    public TimeSpan Duration => _duration;

    /// <inheritdoc/>
    public float Progress => _duration.TotalSeconds > 0 ? (float)(_currentTime.TotalSeconds / _duration.TotalSeconds) : 0;

    /// <inheritdoc/>
    public PlaybackState PlaybackState => _state;

    /// <inheritdoc/>
    public Song Song => _song;

    /// <inheritdoc/>
    public async void Play() {
        if (_state == PlaybackState.Stopped) {
            await _client.LoadAsync(new Media {
                ContentUrl = _url,
                ContentType = "audio/mpeg",
                Metadata = new MusicTrackMetadata {
                    Title = _song.Title,
                    Artist = _song.Artist.Name
                }
            });
        } else {
            await _client.PlayAsync();
        }
        UpdateState(PlaybackState.Playing);
    }

    /// <inheritdoc/>
    public async void Pause() {
        await _client.PauseAsync();
        UpdateState(PlaybackState.Paused);
    }

    /// <inheritdoc/>
    public async void Stop() {
        await _client.StopAsync();
        UpdateState(PlaybackState.Stopped);
    }

    /// <inheritdoc/>
    public async void Seek(TimeSpan position) {
        await _client.SeekAsync(position.TotalSeconds);
    }

    private void UpdateState(PlaybackState newState) {
        if (_state != newState) {
            _state = newState;
            PlaybackStateChanged?.Invoke(this, _state);
        }
    }

    private void OnMediaStatusChanged(object? sender, MediaStatus e) {
        _currentTime = TimeSpan.FromSeconds(e.CurrentTime);
        if (e.Media?.Duration != null) {
            _duration = TimeSpan.FromSeconds(e.Media.Duration.Value);
        }
        
        PositionChanged?.Invoke(this, _currentTime);

        var playerState = e.PlayerState.ToString();
        var newState = playerState switch {
            "Playing" => PlaybackState.Playing,
            "Paused" => PlaybackState.Paused,
            "Buffering" => PlaybackState.Playing,
            _ => PlaybackState.Stopped
        };

        if (e.IdleReason?.ToString() == "FINISHED") {
            SongEnded?.Invoke(this, EventArgs.Empty);
        }

        UpdateState(newState);
    }

    /// <inheritdoc/>
    public void Dispose() {
        _client.MediaStatusChanged -= OnMediaStatusChanged;
        _stream.Dispose();
        _proxyService.StopProxy();
    }
}