using Smoc.Streaming;

namespace Smoc.Services.Audio;

/// <summary>
/// Service for playing back a single piece of media.
/// </summary>
public interface IPlaybackService : IDisposable {
  /// <summary>
  /// Occurs when the song reaches its end.
  /// </summary>
  event EventHandler? SongEnded;

  /// <summary>
  /// Occurs when the playback position changes.
  /// </summary>
  event EventHandler<TimeSpan>? PositionChanged;

  /// <summary>
  /// Occurs when the playback state changes.
  /// </summary>
  event EventHandler<PlaybackState>? PlaybackStateChanged;

  /// <summary>
  /// Gets the current frequency spectrum data from playback.
  /// </summary>
  float[] SpectrumData { get; }

  /// <summary>
  /// Gets or sets a value indicating whether frequency spectrum analysis is active.
  /// </summary>
  bool IsSpectrumActive { get; set; }

  /// <summary>
  /// Gets the current playback position; or <see cref="TimeSpan.Zero"/> if no song is playing.
  /// </summary>
  TimeSpan CurrentTime { get; }

  /// <summary>
  /// Gets the duration of the current song; or <see cref="TimeSpan.Zero"/> if no song is playing.
  /// </summary>
  TimeSpan Duration { get; }

  /// <summary>
  /// Gets the current playback progress (0.0 to 1.0); or 0 if no song is playing.
  /// </summary>
  float Progress { get; }

  /// <summary>
  /// Gets the current playback state.
  /// </summary>
  PlaybackState PlaybackState { get; }

  /// <summary>
  /// Gets the song this instance can play.
  /// </summary>
  Song Song { get; }

  /// <summary>
  /// Starts playback.
  /// </summary>
  void Play();

  /// <summary>
  /// Pauses playback (retains position).
  /// </summary>
  void Pause();

  /// <summary>
  /// Stops playback (resets position).
  /// </summary>
  void Stop();

  /// <summary>
  /// Seeks to a specific position in the song.
  /// </summary>
  /// <param name="position">The position to seek to.</param>
  void Seek(TimeSpan position);
}
