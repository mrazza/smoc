using Smoc.Streaming;

namespace Smoc.Services;

/// <summary>
/// Service for managing the playback queue.
/// </summary>
public interface IPlaybackQueueService : IDisposable {
  /// <summary>
  /// Occurs when the playback queue changes (songs added, removed, or reordered).
  /// </summary>
  event EventHandler? QueueChanged;

  /// <summary>
  /// Occurs when the currently playing song changes.
  /// </summary>
  event EventHandler<Song?>? SongChanged;

  /// <summary>
  /// Occurs when the playback state (Playing, Paused, Stopped) changes.
  /// </summary>
  event EventHandler<PlaybackState>? PlaybackStateChanged;

  /// <summary>
  /// Occurs when the playback position changes (e.g. during playback).
  /// </summary>
  event EventHandler<TimeSpan>? PositionChanged;

  /// <summary>
  /// Occurs when the master volume level changes.
  /// </summary>
  event EventHandler<float>? VolumeChanged;

  /// <summary>
  /// Gets the current state of playback.
  /// </summary>
  PlaybackState PlaybackState { get; }

  /// <summary>
  /// Gets the currently playing song, or null if no song is playing or the queue is empty.
  /// </summary>
  Song? CurrentSong { get; }

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
  /// Gets the index of the currently playing song in the queue.
  /// </summary>
  int CurrentPlaybackIndex { get; }

  /// <summary>
  /// Gets or sets the master volume (0.0 to 1.0).
  /// </summary>
  float Volume { get; set; }

  /// <summary>
  /// Gets a copy of the current playback queue.
  /// </summary>
  IEnumerable<Song> GetCurrentPlaybackQueue();

  /// <summary>
  /// Adds a song to the queue immediately after the current song.
  /// </summary>
  void QueueNext(Song song);

  /// <summary>
  /// Adds multiple songs to the queue immediately after the current song.
  /// </summary>
  void QueueNext(IEnumerable<Song> songs);

  /// <summary>
  /// Adds a song to the very end of the queue.
  /// </summary>
  void QueueLast(Song song);

  /// <summary>
  /// Adds multiple songs to the very end of the queue.
  /// </summary>
  void QueueLast(IEnumerable<Song> songs);

  /// <summary>
  /// Clears the entire playback queue.
  /// </summary>
  void ClearPlaybackQueue();

  /// <summary>
  /// Skips to a specific track in the queue by index.
  /// </summary>
  Task ChangeTrack(int index);

  /// <summary>
  /// Toggles between playing and paused states.
  /// </summary>
  Task PlayPause();

  /// <summary>
  /// Starts or resumes playback.
  /// </summary>
  Task Play();

  /// <summary>
  /// Pauses playback.
  /// </summary>
  void Pause();

  /// <summary>
  /// Stops playback and resets the playback state to Stopped.
  /// </summary>
  void Stop();

  /// <summary>
  /// Skips to the previous track in the queue or restarts the current track if the skip threshold is not met.
  /// </summary>
  /// <param name="skipIgnoreThreshold">If true, the skip threshold is ignored.</param>
  /// <param name="skipThreshold">The threshold to skip to the previous track; if unset, defaults to 10 seconds.</param>
  Task PreviousTrack(bool skipIgnoreThreshold = false, TimeSpan? skipThreshold = null);

  /// <summary>
  /// Skips to the next track in the queue.
  /// </summary>
  Task NextTrack();
}