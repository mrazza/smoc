using Smoc.Streaming;
using Smoc.Ui;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Codecs.FFMpeg;
using SoundFlow.Structs;
using Terminal.Gui.App;

namespace Smoc.Services;

/// <summary>
/// An IPlayerService implementation that uses SoundFlow for audio playback.
/// </summary>
public sealed class SoundFlowPlayerService : IPlayerService {
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

  /// <inheritdoc/>
  public event EventHandler<float>? VolumeChanged;

  /// <inheritdoc/>
  public event EventHandler<Song>? SongChanged;

  /// <inheritdoc/>
  public event EventHandler<PlaybackState>? PlaybackStateChanged;

  /// <inheritdoc/>
  public event EventHandler<TimeSpan>? PositionChanged;

  /// <inheritdoc/>
  public event EventHandler? QueueChanged;

  /// <inheritdoc/>
  public PlaybackState PlaybackState => this._playbackState;

  /// <inheritdoc/>
  public Song? CurrentSong => GetCurrentSong();

  /// <inheritdoc/>
  public TimeSpan CurrentTime => this._streamPlaybackService?.Time ?? TimeSpan.Zero;

  /// <inheritdoc/>
  public TimeSpan Duration => this._streamPlaybackService?.Duration ?? TimeSpan.Zero;

  /// <inheritdoc/>
  public float Progress => this._streamPlaybackService?.Progress ?? 0;

  /// <inheritdoc/>
  public IEnumerable<Song> GetCurrentPlaybackQueue() => _playbackQueue.ToList();

  /// <inheritdoc/>
  public int CurrentPlaybackIndex => _currentPlaybackIndex;

  /// <inheritdoc/>
  public float Volume {
    get => this._playbackDevice.MasterMixer.Volume;
    set {
      this._playbackDevice.MasterMixer.Volume = value;
      InvokeAppEvent(VolumeChanged, value);
    }
  }

  public SoundFlowPlayerService(MainWindow mainWindow, IStreamingClient streamingClient) {
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

  public void QueueNext(Song song) {
    if (_playbackQueue.Count == 0) {
      QueueLast(song);
      return;
    }

    var insertIndex = _currentPlaybackIndex + 1;
    if (insertIndex > _playbackQueue.Count) {
      _playbackQueue.Add(song);
    } else {
      _playbackQueue.Insert(insertIndex, song);
    }
    InvokeAppEvent(QueueChanged);
  }

  /// <inheritdoc/>
  public void QueueNext(IEnumerable<Song> songs) {
    if (_playbackQueue.Count == 0) {
      QueueLast(songs);
      return;
    }

    var insertIndex = _currentPlaybackIndex + 1;
    if (insertIndex > _playbackQueue.Count) {
      _playbackQueue.AddRange(songs);
    } else {
      _playbackQueue.InsertRange(insertIndex, songs);
    }
    InvokeAppEvent(QueueChanged);
  }

  /// <inheritdoc/>
  public void QueueLast(Song song) => QueueLast(song);

  /// <inheritdoc/>
  public void QueueLast(IEnumerable<Song> songs) {
    _playbackQueue.AddRange(songs);
    InvokeAppEvent(QueueChanged);
  }

  /// <inheritdoc/>
  public async Task ChangeTrack(int index) {
    if (index < 0 || index >= _playbackQueue.Count) {
      throw new ArgumentOutOfRangeException(nameof(index));
    }

    Stop();
    _currentPlaybackIndex = index;
    await Play();
  }

  /// <inheritdoc/>
  public void ClearPlaybackQueue() {
    _playbackQueue.Clear();
    InvokeAppEvent(QueueChanged);
  }

  /// <inheritdoc/>
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

  /// <inheritdoc/>
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

  /// <inheritdoc/>
  public void Pause() {
    if (_playbackState != PlaybackState.Playing) {
      return;
    }

    Logging.Debug("Pausing playback...");

    _streamPlaybackService?.Pause();
    _playbackState = PlaybackState.Paused;
    InvokeAppEvent(PlaybackStateChanged, _playbackState);
  }

  /// <inheritdoc/>
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
    } else {
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
    } catch (OperationCanceledException) {
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
