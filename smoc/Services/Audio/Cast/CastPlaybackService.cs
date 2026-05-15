using SharpCaster.Models;
using SharpCaster.Models.ChromecastStatus;
using SharpCaster.Models.MediaStatus;
using SharpCaster.Services;
using Smoc.Services.Cast;
using Smoc.Streaming;

namespace Smoc.Services.Audio.Cast;

public sealed class CastPlaybackService : IPlaybackService {
    private readonly SharpCaster.ChromeCastClient _client;
    private readonly Song _song;
    private readonly Stream _stream;
    private readonly string _url;
    private readonly IStreamingProxyService _proxyService;
    private PlaybackState _state = PlaybackState.Stopped;
    private TimeSpan _currentTime = TimeSpan.Zero;
    private TimeSpan _duration = TimeSpan.Zero;

    public event EventHandler? SongEnded;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<PlaybackState>? PlaybackStateChanged;

    public CastPlaybackService(SharpCaster.ChromeCastClient client, Song song, Stream stream, string url, IStreamingProxyService proxyService) {
        _client = client;
        _song = song;
        _stream = stream;
        _url = url;
        _proxyService = proxyService;

        _client.MediaStatusChanged += OnMediaStatusChanged;
    }

    public TimeSpan CurrentTime => _currentTime;
    public TimeSpan Duration => _duration;
    public float Progress => _duration.TotalSeconds > 0 ? (float)(_currentTime.TotalSeconds / _duration.TotalSeconds) : 0;
    public PlaybackState PlaybackState => _state;
    public Song Song => _song;

    public void Play() {
        UpdateState(PlaybackState.Playing);
    }

    public void Pause() {
        UpdateState(PlaybackState.Paused);
    }

    public void Stop() {
        UpdateState(PlaybackState.Stopped);
    }

    public void Seek(TimeSpan position) {
    }

    private void UpdateState(PlaybackState newState) {
        if (_state != newState) {
            _state = newState;
            PlaybackStateChanged?.Invoke(this, _state);
        }
    }

    private void OnMediaStatusChanged(object? sender, MediaStatus e) {
        _currentTime = TimeSpan.FromSeconds(e.CurrentTime);
        // Try to find duration. It might be on e.Media or e directly.
        // For now, let's just avoid the error.
        PositionChanged?.Invoke(this, _currentTime);

        var playerState = e.PlayerState.ToString();
        var newState = playerState switch {
            "PLAYING" => PlaybackState.Playing,
            "PAUSED" => PlaybackState.Paused,
            "BUFFERING" => PlaybackState.Playing,
            _ => PlaybackState.Stopped
        };

        if (e.IdleReason.ToString().Equals("FINISHED", StringComparison.OrdinalIgnoreCase)) {
            SongEnded?.Invoke(this, EventArgs.Empty);
        }

        UpdateState(newState);
    }

    public void Dispose() {
        _client.MediaStatusChanged -= OnMediaStatusChanged;
        _stream.Dispose();
        _proxyService.StopProxy();
    }
}