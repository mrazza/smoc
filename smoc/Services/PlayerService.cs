using Smoc.Streaming;
using Smoc.Ui;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Codecs.FFMpeg;
using SoundFlow.Structs;
using Terminal.Gui.App;

namespace Smoc.Services;

/// <summary>
/// Orchestrates audio playback, queue management, and interaction with the audio playback engine.
/// </summary>
public sealed class PlayerService : IDisposable {
  private readonly MainWindow _mainWindow;
  private readonly IStreamingClient _streamingClient;
  private readonly MiniAudioEngine _audioEngine;
  private readonly DeviceInfo _playbackDeviceInfo;
  private readonly AudioPlaybackDevice _playbackDevice;

  private readonly List<Song> _playbackQueue;
  private int _currentPlaybackIndex;
  private PlaybackState _playbackState;
  private StreamPlaybackService? _streamPlaybackService;
  private CancellationTokenSource? _playbackCts;

  /// <summary>
  /// Occurs when the master volume level changes.
  /// </summary>
  public event EventHandler<float>? VolumeChanged;

  /// <summary>
  /// Occurs when the currently playing song changes.
  /// </summary>
  public event EventHandler<Song>? SongChanged;

  /// <summary>
  /// Occurs when the playback state (Playing, Paused, Stopped) changes.
  /// </summary>
  public event EventHandler<PlaybackState>? PlaybackStateChanged;

  /// <summary>
  /// Occurs when the playback position changes (e.g. during playback).
  /// </summary>
  public event EventHandler<TimeSpan>? PositionChanged;

  /// <summary>
  /// Occurs when the playback queue changes (songs added, removed, or reordered).
  /// </summary>
  public event EventHandler? QueueChanged;

  /// <summary>
  /// Gets the current state of playback.
  /// </summary>
  public PlaybackState PlaybackState => this._playbackState;

  /// <summary>
  /// Gets the currently playing song, or null if no song is playing or the queue is empty.
  /// </summary>
  public Song? CurrentSong => GetCurrentSong();

  /// <summary>
  /// Gets the current playback position; or <see cref="TimeSpan.Zero"/> if no song is playing.
  /// </summary>
  public TimeSpan CurrentTime => this._streamPlaybackService?.Time ?? TimeSpan.Zero;

  /// <summary>
  /// Gets the duration of the current song; or <see cref="TimeSpan.Zero"/> if no song is playing.
  /// </summary>
  public TimeSpan Duration => this._streamPlaybackService?.Duration ?? TimeSpan.Zero;

  /// <summary>
  /// Gets the current playback progress (0.0 to 1.0); or 0 if no song is playing.
  /// </summary>
  public float Progress => this._streamPlaybackService?.Progress ?? 0;

  /// <summary>
  /// Gets a copy of the current playback queue.
  /// </summary>
  public IEnumerable<Song> GetCurrentPlaybackQueue() => _playbackQueue.ToList();

  /// <summary>
  /// Gets the index of the currently playing song in the queue.
  /// </summary>
  public int CurrentPlaybackIndex => _currentPlaybackIndex;

  /// <summary>
  /// Gets or sets the master volume (0.0 to 1.0).
  /// </summary>
  public float Volume {
    get => this._playbackDevice.MasterMixer.Volume;
    set {
      this._playbackDevice.MasterMixer.Volume = value;
      InvokeAppEvent(VolumeChanged, value);
    }
  }

  public PlayerService(MainWindow mainWindow, IStreamingClient streamingClient) {
    _mainWindow = mainWindow;
    _streamingClient = streamingClient;
    _audioEngine = new MiniAudioEngine();
    _audioEngine.RegisterCodecFactory(new FFmpegCodecFactory());
    _audioEngine.UpdateAudioDevicesInfo();
    _playbackDeviceInfo = _audioEngine.PlaybackDevices.FirstOrDefault(x => x.IsDefault);
    _playbackDevice = _audioEngine.InitializePlaybackDevice(_playbackDeviceInfo, AudioFormat.DvdHq);
    _playbackDevice.Start();

    _playbackQueue = new List<Song>();
    _currentPlaybackIndex = 0;
    _playbackState = PlaybackState.Stopped;
    _streamPlaybackService = null;
  }

  /// <summary>
  /// Adds a song to the queue immediately after the current song (or at the end if no song is playing).
  /// </summary>
  /// <param name="song">The song to queue next.</param>
  public void QueueSong(Song song) => QueueSongs([song]);

  public void QueueSongs(IEnumerable<Song> songs) {
    _playbackQueue.AddRange(songs);
    InvokeAppEvent(QueueChanged);
  }

  public void QueueNext(Song song) {
    if (_playbackQueue.Count == 0) {
      QueueSong(song);
      return;
    }

    var insertIndex = _currentPlaybackIndex + 1;
    if (insertIndex > _playbackQueue.Count) {
      _playbackQueue.Add(song);
    }
    else {
      _playbackQueue.Insert(insertIndex, song);
    }
    InvokeAppEvent(QueueChanged);
  }

  /// <summary>
  /// Adds multiple songs to the queue immediately after the current song.
  /// </summary>
  /// <param name="songs">The songs to queue next.</param>
  public void QueueNext(IEnumerable<Song> songs) {
    if (_playbackQueue.Count == 0) {
      QueueSongs(songs);
      return;
    }

    var insertIndex = _currentPlaybackIndex + 1;
    if (insertIndex > _playbackQueue.Count) {
      _playbackQueue.AddRange(songs);
    }
    else {
      _playbackQueue.InsertRange(insertIndex, songs);
    }
    InvokeAppEvent(QueueChanged);
  }

  /// <summary>
  /// Adds a song to the very end of the queue.
  /// </summary>
  /// <param name="song">The song to add.</param>
  public void QueueLast(Song song) => QueueSong(song);

  /// <summary>
  /// Adds multiple songs to the very end of the queue.
  /// </summary>
  /// <param name="songs">The songs to add.</param>
  public void QueueLast(IEnumerable<Song> songs) => QueueSongs(songs);

  /// <summary>
  /// Skips to a specific track in the queue by index.
  /// </summary>
  /// <param name="index">The zero-based index of the track in the queue.</param>
  /// <exception cref="ArgumentOutOfRangeException">Thrown if the index is out of range.</exception>
  public async Task ChangeTrack(int index) {
    if (index < 0 || index >= _playbackQueue.Count) {
      throw new ArgumentOutOfRangeException(nameof(index));
    }

    Stop();
    _currentPlaybackIndex = index;
    await Play();
  }

  /// <summary>
  /// Clears the entire playback queue.
  /// </summary>
  public void ClearPlaybackQueue() {
    _playbackQueue.Clear();
    InvokeAppEvent(QueueChanged);
  }

  /// <summary>
  /// Toggles between playing and paused states.
  /// </summary>
  public async Task PlayPause() {
    switch (_playbackState) {
      case PlaybackState.Playing:
        Pause();
        break;
      case PlaybackState.Paused:
        await Play();
        break;
    }
  }

  /// <summary>
  /// Starts or resumes playback.
  /// </summary>
  public async Task Play() {
    switch (_playbackState) {
      case PlaybackState.Paused:
        Logging.Debug("Resuming playback...");
        _streamPlaybackService?.Play();
        _playbackState = PlaybackState.Playing;
        InvokeAppEvent(PlaybackStateChanged, _playbackState);
        return;
      case PlaybackState.Stopped:
        if (_playbackQueue.Count == 0) {
          Logging.Debug("No songs in queue, cannot start playback.");
          return;
        }

        Logging.Debug("Starting playback...");
        _streamPlaybackService?.Dispose();
        _playbackState = PlaybackState.Playing;
        InvokeAppEvent(PlaybackStateChanged, _playbackState);
        await PlayCurrentSong();
        return;
    }

    Logging.Debug($"Playback requested when in invalid state {_playbackState}.");
  }

  /// <summary>
  /// Pauses playback.
  /// </summary>
  public void Pause() {
    if (_playbackState != PlaybackState.Playing) {
      return;
    }

    Logging.Debug("Pausing playback...");

    _streamPlaybackService?.Pause();
    _playbackState = PlaybackState.Paused;
    InvokeAppEvent(PlaybackStateChanged, _playbackState);
  }

  /// <summary>
  /// Stops playback and resets the playback state to Stopped.
  /// </summary>
  public void Stop() {
    if (_playbackState == PlaybackState.Stopped) {
      return;
    }

    Logging.Debug("Stopping playback...");

    _playbackState = PlaybackState.Stopped;
    InvokeAppEvent(PlaybackStateChanged, _playbackState);
    _streamPlaybackService?.Dispose();
    _streamPlaybackService = null;
  }

  private async void OnStreamEnded(object? sender, EventArgs e) {
    Logging.Debug($"Stream ended for {CurrentSong?.Title} ({CurrentSong?.Id}).");
    _streamPlaybackService?.Dispose();
    _streamPlaybackService = null;

    if (++_currentPlaybackIndex >= _playbackQueue.Count) {
      Logging.Debug($"Reached the end of the queue, stopping playback.");
      _currentPlaybackIndex = 0;
      _playbackState = PlaybackState.Stopped;
      InvokeAppEvent(PlaybackStateChanged, _playbackState);
    }
    else {
      Logging.Debug($"Playing next song...");
      await PlayCurrentSong();
    }
  }

  public void Dispose() {
    Stop();
    _playbackCts?.Cancel();
    _playbackCts?.Dispose();
    _playbackDevice.Dispose();
    _audioEngine.Dispose();
  }

  private Song? GetCurrentSong() {
    if (_currentPlaybackIndex >= _playbackQueue.Count) {
      return null;
    }

    return _playbackQueue[_currentPlaybackIndex];
  }

  private async Task PlayCurrentSong() {
    if (GetCurrentSong() is not Song currentSong) {
      throw new InvalidOperationException("No song in queue");
    }

    // Cancel any previous playback setup
    _playbackCts?.Cancel();
    _playbackCts?.Dispose();
    _playbackCts = new CancellationTokenSource();
    var token = _playbackCts.Token;

    try {
      Logging.Debug($"Starting playback for {currentSong.Title} ({currentSong.Id})...");
      var songStream = await _streamingClient.GetSongStreamAsync(currentSong.Id, token);

      if (token.IsCancellationRequested) return;

      Logging.Debug($"Received stream for {currentSong.Title} ({currentSong.Id}), decoding format...");

      var codec = songStream.Codec;
      if (codec.StartsWith("mp4a")) {
        codec = "m4a";
      }

      // Check again before expensive operations
      if (token.IsCancellationRequested) return;

      using var decoder = _audioEngine.CreateDecoder(songStream.Stream, codec, AudioFormat.DvdHq);

      var format = new AudioFormat {
        Format = decoder.SampleFormat,
        Channels = decoder.Channels,
        SampleRate = decoder.SampleRate,
        Layout = AudioFormat.GetLayoutFromChannels(decoder.Channels)
      };
      Logging.Debug($"Decoded format for {currentSong.Title} ({currentSong.Id}): {format.Format}, {format.Channels}, {format.SampleRate}, {format.Layout}");

      // Final check before starting playback service
      if (token.IsCancellationRequested) return;

      _streamPlaybackService = new StreamPlaybackService(_audioEngine, _playbackDevice, songStream.Stream, format);
      _streamPlaybackService.StreamEnded += OnStreamEnded;
      _streamPlaybackService.PositionChanged += (sender, args) => InvokeAppEvent(PositionChanged, args);
      _streamPlaybackService.Play();
      InvokeAppEvent(SongChanged, currentSong);
    }
    catch (OperationCanceledException) {
      Logging.Debug($"Playback setup for {currentSong.Title} cancelled.");
    }
  }

  /// <summary>
  /// Invokes an event handler on the UI thread.
  /// </summary>
  /// <remarks>
  /// This is required because many events from the underlying SoundFlow playback system
  /// can be triggered for audio-specific threads and subscribers will expect all event
  /// handlers to marshal back to the UI thread.
  /// </remarks>
  /// <param name="eventHandler">The event handler to invoke.</param>
  private void InvokeAppEvent(EventHandler? eventHandler) {
    _mainWindow.App?.Invoke(() => eventHandler?.Invoke(this, EventArgs.Empty));
  }

  /// <summary>
  /// Invokes an event handler on the UI thread.
  /// </summary>
  /// <remarks>
  /// This is required because many events from the underlying SoundFlow playback system
  /// can be triggered for audio-specific threads and subscribers will expect all event
  /// handlers to marshal back to the UI thread.
  /// </remarks>
  /// <param name="eventHandler">The event handler to invoke.</param>
  /// <param name="args">The arguments to pass to the event handler.</param>
  private void InvokeAppEvent<T>(EventHandler<T>? eventHandler, T args) {
    _mainWindow.App?.Invoke(() => eventHandler?.Invoke(this, args));
  }
}
