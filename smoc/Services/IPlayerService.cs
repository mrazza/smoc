using Smoc.Streaming;
using Smoc.Ui;
using Smoc.Ui.Components;

namespace Smoc.Services;

/// <summary>
/// Orchestrates audio playback, queue management, and interaction with the audio playback engine.
/// </summary>
public interface IPlayerService : IDisposable {
  /// <summary>
  /// Occurs when the playback queue changes (songs added, removed, or reordered).
  /// </summary>
  event EventHandler? QueueChanged;

  /// <summary>
  /// Occurs when the currently playing song changes.
  /// </summary>
  event EventHandler<Song>? SongChanged;

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
  /// Adds a song to the queue immediately after the current song (or at the end if no song is playing).
  /// </summary>
  void QueueSong(Song song);

  /// <summary>
  /// Adds multiple songs to the queue immediately after the current song (or at the end if no song is playing).
  /// </summary>
  void QueueSongs(IEnumerable<Song> songs);

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
  /// Skips to a specific track in the queue by index.
  /// </summary>
  Task ChangeTrack(int index);

  /// <summary>
  /// Clears the entire playback queue.
  /// </summary>
  void ClearPlaybackQueue();

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
}
