using Smoc.Services.Audio;
using Smoc.Services.Util;
using Smoc.Streaming;
using Smoc.Ui;
using Terminal.Gui.App;

namespace Smoc.Services;

public sealed class StandardPlaybackQueueService : IPlaybackQueueService {
  private readonly IAudioService _audioService;
  private readonly IMainWindow _mainWindow;
  private readonly IStreamingClient _streamingClient;

  private readonly UniqueResource<IPlaybackService> _playbackService;
  private readonly UniqueResource<IPlaybackService> _preloadedPlaybackService;
  private readonly List<Song> _playbackQueue;
  private int _currentPlaybackIndex;
  private readonly UniqueResource<CancellationTokenSource> _playbackCts;
  private readonly UniqueResource<CancellationTokenSource> _preloadCts;
  private Song? _preloadingSong;
  private Task<IPlaybackService>? _preloadingTask;

  /// <inheritdoc/>
  public event EventHandler<float>? VolumeChanged;

  /// <inheritdoc/>
  public event EventHandler<Song?>? SongChanged;

  /// <inheritdoc/>
  public event EventHandler<PlaybackState>? PlaybackStateChanged;

  /// <inheritdoc/>
  public event EventHandler<TimeSpan>? PositionChanged;

  /// <inheritdoc/>
  public event EventHandler? QueueChanged;

  /// <inheritdoc/>
  public PlaybackState PlaybackState => _playbackService.Resource?.PlaybackState ?? PlaybackState.Stopped;

  /// <inheritdoc/>
  public Song? CurrentSong => GetCurrentSong();

  /// <inheritdoc/>
  public TimeSpan CurrentTime => _playbackService.Resource?.CurrentTime ?? TimeSpan.Zero;

  /// <inheritdoc/>
  public TimeSpan Duration => _playbackService.Resource?.Duration ?? TimeSpan.Zero;

  /// <inheritdoc/>
  public float Progress => _playbackService.Resource?.Progress ?? 0;

  /// <inheritdoc/>
  public float[] SpectrumData => _playbackService.Resource?.SpectrumData ?? Array.Empty<float>();

  /// <inheritdoc/>
  public bool IsSpectrumActive {
    get => _playbackService.Resource?.IsSpectrumActive ?? false;
    set {
      if (_playbackService.Resource != null) {
        _playbackService.Resource.IsSpectrumActive = value;
      }
    }
  }

  /// <inheritdoc/>
  public IEnumerable<Song> GetCurrentPlaybackQueue() => _playbackQueue.ToList();

  /// <inheritdoc/>
  public int CurrentPlaybackIndex => _currentPlaybackIndex;

  /// <inheritdoc/>
  public float Volume {
    get => _audioService.Volume;
    set {
      _audioService.Volume = value;
      InvokeAppEvent(VolumeChanged, value);
    }
  }

  /// <summary>
  /// Creates a new instance of <see cref="StandardPlaybackQueueService"/>.
  /// </summary>
  /// <param name="mainWindow">The main window to use.</param>
  /// <param name="streamingClient">The streaming client to use.</param>
  /// <param name="audioService">The audio service to use.</param>
  public StandardPlaybackQueueService(IMainWindow mainWindow, IStreamingClient streamingClient, IAudioService audioService) {
    _mainWindow = mainWindow;
    _streamingClient = streamingClient;
    _audioService = audioService;

    _playbackQueue = [];
    _currentPlaybackIndex = 0;
    _playbackCts = new UniqueResource<CancellationTokenSource>((token) => token.Cancel());
    _preloadCts = new UniqueResource<CancellationTokenSource>((token) => token.Cancel());
    _preloadedPlaybackService = new UniqueResource<IPlaybackService>((service) => {
      service.PlaybackStateChanged -= OnPlaybackStateChanged;
      service.PositionChanged -= OnPositionChanged;
      service.SongEnded -= OnSongEnded;
    });
    _playbackService = new UniqueResource<IPlaybackService>((service) => {
      service.PlaybackStateChanged -= OnPlaybackStateChanged;
      service.PositionChanged -= OnPositionChanged;
      service.SongEnded -= OnSongEnded;
    });
  }

  /// <inheritdoc/>
  public void QueueNext(Song song) => QueueNext([song]);

  /// <inheritdoc/>
  public void QueueNext(IEnumerable<Song> songs) {
    if (!songs.Any()) return;

    if (_playbackQueue.Count == 0) {
      QueueLast(songs);
      return;
    }

    var wasEmpty = _playbackQueue.Count == 0;
    var insertIndex = _currentPlaybackIndex + 1;
    if (insertIndex >= _playbackQueue.Count) {
      _playbackQueue.AddRange(songs);
    } else {
      _playbackQueue.InsertRange(insertIndex, songs);
    }
    if (wasEmpty) InvokeAppEvent(SongChanged, GetCurrentSong()!);
    InvokeAppEvent(QueueChanged);
    EnsurePreload();
  }

  /// <inheritdoc/>
  public void QueueLast(Song song) => QueueLast([song]);

  /// <inheritdoc/>
  public void QueueLast(IEnumerable<Song> songs) {
    if (!songs.Any()) return;
    var wasEmpty = _playbackQueue.Count == 0;
    _playbackQueue.AddRange(songs);
    if (wasEmpty) InvokeAppEvent(SongChanged, GetCurrentSong()!);
    InvokeAppEvent(QueueChanged);
    EnsurePreload();
  }

  /// <inheritdoc/>
  public void ClearPlaybackQueue() {
    if (_playbackQueue.Count == 0) return;

    _playbackQueue.Clear();
    _currentPlaybackIndex = 0;
    InvokeAppEvent(QueueChanged);
    InvokeAppEvent(SongChanged, null);
    EnsurePreload();
  }

  /// <inheritdoc/>
  public async Task ChangeTrack(int index) {
    if (index < 0 || index >= _playbackQueue.Count) {
      throw new ArgumentOutOfRangeException(nameof(index));
    }

    if (_currentPlaybackIndex == index) return;

    var wasPlaying = PlaybackState == PlaybackState.Playing;

    Stop();
    _currentPlaybackIndex = index;
    InvokeAppEvent(SongChanged, GetCurrentSong()!);

    if (wasPlaying) await Play();
  }

  /// <inheritdoc/>
  public async Task PlayPause() {
    switch (PlaybackState) {
      case PlaybackState.Playing:
        Pause();
        break;
      case PlaybackState.Paused:
      case PlaybackState.Stopped:
        await Play();
        break;
    }
  }

  /// <inheritdoc/>
  public async Task Play() {
    if (PlaybackState == PlaybackState.Playing) return;

    if (_playbackService.Resource is { } && _playbackService.Resource.Song == GetCurrentSong()) {
      _playbackService.Resource.Play();
      EnsurePreload();
      return;
    }

    if (GetCurrentSong() is not { } currentSong) {
      throw new InvalidOperationException("No song in queue");
    }

    // Cancel any previous playback setup
    var token = _playbackCts.Replace(new CancellationTokenSource()).Token;

    try {
      IPlaybackService playback;

      if (_preloadingSong == currentSong && _preloadedPlaybackService.Resource is { } preloaded) {
        Logging.Debug($"Using preloaded playback service for {currentSong.Title} ({currentSong.Id})...");
        playback = _preloadedPlaybackService.Release()!;
        _preloadingSong = null;
        _preloadingTask = null;
      } else if (_preloadingSong == currentSong && _preloadingTask is { } preloadTask) {
        Logging.Debug($"Waiting for preloading task for {currentSong.Title} ({currentSong.Id})...");
        playback = await preloadTask;
        if (_preloadedPlaybackService.Resource == playback) {
          playback = _preloadedPlaybackService.Release()!;
        }
        _preloadingSong = null;
        _preloadingTask = null;
      } else {
        Logging.Debug($"Starting playback for {currentSong.Title} ({currentSong.Id})...");
        var songStream = await _streamingClient.GetSongStreamAsync(currentSong.Id, token);

        token.ThrowIfCancellationRequested();

        Logging.Debug($"Received stream for {currentSong.Title} ({currentSong.Id}), decoding format...");

        var codec = songStream.Codec;
        if (codec.StartsWith("mp4a")) {
          codec = "m4a";
        }

        token.ThrowIfCancellationRequested();

        Logging.Debug($"Creating playing service for {currentSong.Title} ({currentSong.Id})...");
        playback = _audioService.MakePlaybackService(currentSong, songStream.Stream, codec, token);
      }

      _playbackService.Replace(playback);
      playback.SongEnded += OnSongEnded;
      playback.PositionChanged += OnPositionChanged;
      playback.PlaybackStateChanged += OnPlaybackStateChanged;

      token.ThrowIfCancellationRequested();
      playback.Play();

      EnsurePreload();
    } catch (OperationCanceledException) {
      Logging.Debug($"Playback setup for {currentSong.Title} cancelled.");
    } catch (Exception e) {
      Logging.Error($"Playback setup for {currentSong.Title} failed: {e.Message}");
    }
  }

  /// <inheritdoc/>
  public void Pause() {
    _playbackService.Resource?.Pause();
  }

  /// <inheritdoc/>
  public void Stop() {
    _playbackService.Resource?.Stop();
  }

  /// <inheritdoc/>
  public async Task PreviousTrack(bool skipIgnoreThreshold = false, TimeSpan? skipThreshold = null) {
    if (!skipIgnoreThreshold && CurrentTime > (skipThreshold ?? TimeSpan.FromSeconds(10))) {
      Logging.Debug("Previous song above threshold, going to start...");
      SeekTo(TimeSpan.Zero);
    } else if (_currentPlaybackIndex > 0) {
      Logging.Debug("Previous song below threshold, going to previous...");
      await ChangeTrack(_currentPlaybackIndex - 1);
      await Play();
    } else {
      Logging.Debug("Previous song below threshold, no previous song, stopping playback...");
      Stop();
    }
  }

  /// <inheritdoc/>
  public async Task NextTrack() {
    Logging.Debug($"Playing next song...");
    if (_currentPlaybackIndex + 1 < _playbackQueue.Count) {
      Logging.Debug($"Playing next song...");
      await ChangeTrack(_currentPlaybackIndex + 1);
      await Play();
    } else {
      Logging.Debug($"Reached the end of the queue, stopping playback.");
      Stop();
    }
  }

  private void EnsurePreload() {
    var nextSongIndex = _currentPlaybackIndex + 1;
    if (nextSongIndex >= _playbackQueue.Count) {
      _preloadingSong = null;
      _preloadingTask = null;
      _preloadedPlaybackService.Replace(null!);
      _preloadCts.Replace(new CancellationTokenSource());
      return;
    }

    var nextSong = _playbackQueue[nextSongIndex];
    if (_preloadingSong == nextSong) return;

    _preloadedPlaybackService.Replace(null!);
    _preloadingSong = nextSong;
    var token = _preloadCts.Replace(new CancellationTokenSource()).Token;
    _preloadingTask = PreloadTrackImpl(nextSong, token);
  }

  private async Task<IPlaybackService> PreloadTrackImpl(Song song, CancellationToken token) {
    try {
      Logging.Debug($"Preloading stream for {song.Title} ({song.Id})...");
      var songStream = await _streamingClient.GetSongStreamAsync(song.Id, token);
      token.ThrowIfCancellationRequested();

      var codec = songStream.Codec;
      if (codec.StartsWith("mp4a")) {
        codec = "m4a";
      }

      var playback = _audioService.MakePlaybackService(song, songStream.Stream, codec, token);
      _preloadedPlaybackService.Replace(playback);
      token.ThrowIfCancellationRequested();

      return playback;
    } catch (OperationCanceledException) {
      Logging.Debug($"Preload cancelled for {song.Title}.");
      throw;
    } catch (Exception e) {
      Logging.Error($"Preload failed for {song.Title}: {e.Message}");
      throw;
    }
  }

  /// <inheritdoc/>
  public void SeekTo(TimeSpan position) {
    if (_playbackService.Resource is not { } playback) {
      Logging.Debug("Can't seek, no current song (no playback resource)");
      return;
    }
    ArgumentOutOfRangeException.ThrowIfGreaterThan(position, Duration);
    playback.Seek(position);
  }

  /// <inheritdoc/>
  public void SeekForward(TimeSpan duration) {
    var targetPosition = CurrentTime + duration;
    SeekTo(targetPosition > Duration ? Duration : targetPosition);
  }

  /// <inheritdoc/>
  public void SeekBackward(TimeSpan duration) {
    var targetPosition = CurrentTime - duration;
    SeekTo(targetPosition < TimeSpan.Zero ? TimeSpan.Zero : targetPosition);
  }

  /// <inheritdoc/>
  public void Dispose() {
    _playbackCts.Dispose();
    _preloadCts.Dispose();
    _preloadedPlaybackService.Dispose();
    _playbackService.Dispose();
    _audioService.Dispose();
  }

  private async void OnSongEnded(object? sender, EventArgs e) {
    Logging.Debug($"Playback ended for {CurrentSong?.Title} ({CurrentSong?.Id}).");
    _preloadedPlaybackService.Resource?.Play();
    await NextTrack();
  }

  private void OnPositionChanged(object? sender, TimeSpan e) {
    InvokeAppEvent(PositionChanged, e);
  }

  private void OnPlaybackStateChanged(object? sender, PlaybackState e) {
    InvokeAppEvent(PlaybackStateChanged, e);
  }

  private Song? GetCurrentSong() {
    if (_currentPlaybackIndex >= _playbackQueue.Count) {
      return null;
    }

    return _playbackQueue[_currentPlaybackIndex];
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

  /// <summary>
  /// Creates a new instance of <see cref="StandardPlaybackQueueService"/> using a newly created instance of <see cref="IAudioService"/>.
  /// </summary>
  /// <param name="mainWindow">The main window to use.</param>
  /// <param name="streamingClient">The streaming client to use.</param>
  /// <typeparam name="T">The type of <see cref="IAudioService"/> to use.</typeparam>
  public static IPlaybackQueueService UsingAudioService<T>(MainWindow mainWindow, IStreamingClient streamingClient)
    where T : IAudioService, new() {
    return new StandardPlaybackQueueService(mainWindow, streamingClient, new T());
  }
}