using Smoc.Services;
using Smoc.Services.Audio;
using Smoc.Streaming;

namespace smoc.Tests.Fakes;

public class FakePlaybackService(Song song) : IPlaybackService {
  public TimeSpan CurrentTime { get; private set; }

  public TimeSpan Duration => Song.Duration;

  public float Progress => (float)(CurrentTime / Duration);

  public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;

  public Song Song { get; } = song;

  public event EventHandler? SongEnded;
  public event EventHandler<TimeSpan>? PositionChanged;
  public event EventHandler<PlaybackState>? PlaybackStateChanged;

  public bool IsDisposed { get; private set; } = false;

  public void SetCurrentTime(TimeSpan time) {
    if (CurrentTime == time) return;
    CurrentTime = time;
    PositionChanged?.Invoke(this, time);
  }

  public void EndSong() {
    SetCurrentTime(Duration);
    Stop();
    SongEnded?.Invoke(this, EventArgs.Empty);
  }

  public void Dispose() {
    IsDisposed = true;
  }

  public void Pause() {
    if (PlaybackState == PlaybackState.Paused) return;
    PlaybackState = PlaybackState.Paused;
    PlaybackStateChanged?.Invoke(this, PlaybackState);
  }

  public void Play() {
    if (PlaybackState == PlaybackState.Playing) return;
    PlaybackState = PlaybackState.Playing;
    PlaybackStateChanged?.Invoke(this, PlaybackState);
  }

  public void Stop() {
    if (PlaybackState == PlaybackState.Stopped) return;
    PlaybackState = PlaybackState.Stopped;
    PlaybackStateChanged?.Invoke(this, PlaybackState);
  }

  public void Seek(TimeSpan position) {
    if (CurrentTime == position) return;
    if (position > Duration) throw new ArgumentException("Position cannot be greater than duration", nameof(position));
    CurrentTime = position;
    PositionChanged?.Invoke(this, position);
  }
}