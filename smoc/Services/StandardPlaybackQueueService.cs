using System.Net;
using System.Net.Http;
using Smoc.Services.Audio;
using Smoc.Services.Util;
using Smoc.Streaming;
using Smoc.Ui;
using Terminal.Gui.App;

namespace Smoc.Services;

/// <summary>
/// Service managing the playback queue, current song playback lifecycle, preloading, and audio state.
/// </summary>
public sealed class StandardPlaybackQueueService : IPlaybackQueueService {
  private IAudioService _audioService;
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
  private TimeSpan? _pendingRestorePosition;
  private bool _isPlayPending;

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
  public TimeSpan CurrentTime => _playbackService.Resource?.CurrentTime ?? _pendingRestorePosition ?? TimeSpan.Zero;

  /// <inheritdoc/>
  public TimeSpan Duration => _playbackService.Resource?.Duration ?? CurrentSong?.Duration ?? TimeSpan.Zero;

  /// <inheritdoc/>
  public float Progress {
    get {
      if (_playbackService.Resource is { } playback) {
        return playback.Progress;
      }
      if (_pendingRestorePosition is { } pending && Duration > TimeSpan.Zero) {
        return Math.Clamp((float)(pending / Duration), 0f, 1f);
      }
      return 0f;
    }
  }

  /// <inheritdoc/>
  public float[] SpectrumData => _playbackService.Resource?.SpectrumData ?? Array.Empty<float>();

  /// <inheritdoc/>
  public bool IsSpectrumActive {
    get;
    set {
      field = value;
      if (_playbackService.Resource != null) {
        _playbackService.Resource.IsSpectrumActive = value;
      }
    }
  } = false;

  /// <inheritdoc/>
  public IEnumerable<Song> GetCurrentPlaybackQueue() => _playbackQueue.ToList();

  /// <inheritdoc/>
  public int CurrentPlaybackIndex => _currentPlaybackIndex;

  internal Task? PreloadingTask => _preloadingTask;

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
    if (!songs.Any()) {
      return;
    }

    if (_playbackQueue.Count == 0) {
      QueueLast(songs);
      return;
    }

    var insertIndex = _currentPlaybackIndex + 1;
    if (insertIndex >= _playbackQueue.Count) {
      _playbackQueue.AddRange(songs);
    } else {
      _playbackQueue.InsertRange(insertIndex, songs);
    }
    InvokeAppEvent(QueueChanged);
    EnsurePreload();
  }

  /// <inheritdoc/>
  public void QueueLast(Song song) => QueueLast([song]);

  /// <inheritdoc/>
  public void QueueLast(IEnumerable<Song> songs) {
    if (!songs.Any()) {
      return;
    }
    var wasEmpty = _playbackQueue.Count == 0;
    _playbackQueue.AddRange(songs);
    if (wasEmpty) {
      InvokeAppEvent(SongChanged, GetCurrentSong()!);
    }
    InvokeAppEvent(QueueChanged);
    EnsurePreload();
  }

  /// <inheritdoc/>
  public void ClearPlaybackQueue() {
    if (_playbackQueue.Count == 0 && _playbackService.Resource == null && !_isPlayPending) {
      return;
    }

    Stop();
    _playbackCts.Replace(new CancellationTokenSource());
    _playbackService.Replace(null!);
    CancelPreload();

    _playbackQueue.Clear();
    _currentPlaybackIndex = 0;
    _pendingRestorePosition = null;
    InvokeAppEvent(QueueChanged);
    InvokeAppEvent(SongChanged, null);
  }

  /// <inheritdoc/>
  public async Task ChangeTrack(int index) {
    if (index < 0 || index >= _playbackQueue.Count) {
      throw new ArgumentOutOfRangeException(nameof(index));
    }

    if (_currentPlaybackIndex == index && (_playbackService.Resource == null || _playbackService.Resource.Song == _playbackQueue[index])) {
      return;
    }

    var wasPlaying = PlaybackState == PlaybackState.Playing || _isPlayPending;

    Stop();
    _pendingRestorePosition = null;
    _currentPlaybackIndex = index;
    InvokeAppEvent(SongChanged, GetCurrentSong()!);

    if (wasPlaying) {
      await Play();
    }
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
    if (PlaybackState == PlaybackState.Playing && _playbackService.Resource?.Song == GetCurrentSong()) {
      return;
    }

    if (_playbackService.Resource is { } && _playbackService.Resource.Song == GetCurrentSong()) {
      _playbackService.Resource.Play();
      EnsurePreload();
      return;
    }

    if (_playbackService.Resource is { } existingPlayback && existingPlayback.Song != GetCurrentSong()) {
      Stop();
      _playbackService.Replace(null!);
    }

    if (GetCurrentSong() is not { } currentSong) {
      throw new InvalidOperationException("No song in queue");
    }

    _isPlayPending = true;
    // Cancel any previous playback setup
    var token = _playbackCts.Replace(new CancellationTokenSource()).Token;

    try {
      IPlaybackService playback;

      if (_preloadingSong is not null && _preloadingSong != currentSong && _preloadingSong != GetNextSong()) {
        CancelPreload();
      }

      if (_preloadingSong == currentSong && _preloadedPlaybackService.Resource is { }) {
        Logging.Debug($"Using preloaded playback service for {currentSong.Title} ({currentSong.Id})...");
        playback = _preloadedPlaybackService.Release()!;
        _preloadingSong = null;
        _preloadingTask = null;
      } else if (_preloadingSong == currentSong && _preloadingTask is { } preloadTask) {
        Logging.Debug($"Waiting for preloading task for {currentSong.Title} ({currentSong.Id})...");
        try {
          playback = await preloadTask.WaitAsync(token);
        } catch (OperationCanceledException) when (token.IsCancellationRequested) {
          if (_preloadingSong == currentSong) {
            CancelPreload();
          }
          throw;
        } catch {
          if (_preloadingSong == currentSong) {
            CancelPreload();
          }
          Logging.Debug($"Starting playback for {currentSong.Title} ({currentSong.Id}) after preload cancelled...");
          var songStream = await _streamingClient.GetSongStreamAsync(currentSong.Id, token);

          if (token.IsCancellationRequested) {
            songStream.Stream.Dispose();
            token.ThrowIfCancellationRequested();
          }

          Logging.Debug($"Creating playing service for {currentSong.Title} ({currentSong.Id})...");
          playback = await CreatePlaybackServiceAsync(currentSong, songStream, token);
        }
        if (token.IsCancellationRequested) {
          if (_preloadedPlaybackService.Resource == playback) {
            _preloadedPlaybackService.Replace(null!);
          } else {
            playback.Dispose();
          }
          if (_preloadingSong == currentSong) {
            CancelPreload();
          }
          token.ThrowIfCancellationRequested();
        }
        if (_preloadedPlaybackService.Resource == playback) {
          playback = _preloadedPlaybackService.Release()!;
        }
        _preloadingSong = null;
        _preloadingTask = null;
      } else {
        if (_preloadingSong != GetNextSong()) {
          CancelPreload();
        }
        Logging.Debug($"Starting playback for {currentSong.Title} ({currentSong.Id})...");
        var songStream = await _streamingClient.GetSongStreamAsync(currentSong.Id, token);

        if (token.IsCancellationRequested) {
          songStream.Stream.Dispose();
          token.ThrowIfCancellationRequested();
        }

        Logging.Debug($"Creating playing service for {currentSong.Title} ({currentSong.Id})...");
        playback = await CreatePlaybackServiceAsync(currentSong, songStream, token);
      }

      if (token.IsCancellationRequested) {
        playback.Dispose();
        token.ThrowIfCancellationRequested();
      }

      _playbackService.Replace(playback);
      playback.IsSpectrumActive = IsSpectrumActive;
      playback.SongEnded += OnSongEnded;
      playback.PositionChanged += OnPositionChanged;
      playback.PlaybackStateChanged += OnPlaybackStateChanged;

      token.ThrowIfCancellationRequested();
      if (_pendingRestorePosition is { } restorePos) {
        _pendingRestorePosition = null;
        if (restorePos > TimeSpan.Zero && restorePos <= playback.Duration) {
          playback.Seek(restorePos);
        }
      }
      playback.Play();
      _isPlayPending = false;

      EnsurePreload();
    } catch (OperationCanceledException) {
      if (_playbackCts.Resource?.Token == token) {
        _isPlayPending = false;
      }
      Logging.Debug($"Playback setup for {currentSong.Title} cancelled.");
    } catch (Exception e) {
      if (_playbackCts.Resource?.Token == token) {
        _isPlayPending = false;
      }
      Logging.Error($"Playback setup for {currentSong.Title} failed: {e}");
      if (IsForbiddenHttpError(e)) {
        _mainWindow.DisplayError(
          $"Playback failed for '{currentSong.Title}' (403 Forbidden). Tokens or cookies may be expired; try running with --gentokens.");
      } else {
        _mainWindow.DisplayError($"Playback failed for '{currentSong.Title}': {e.Message}");
      }
    }
  }

  /// <inheritdoc/>
  public void Pause() {
    _isPlayPending = false;
    _playbackService.Resource?.Pause();
  }

  /// <inheritdoc/>
  public void Stop() {
    _isPlayPending = false;
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
      if (PlaybackState != PlaybackState.Playing && !_isPlayPending) {
        await Play();
      }
    } else {
      Logging.Debug("Previous song below threshold, no previous song, stopping playback...");
      Stop();
    }
  }

  /// <inheritdoc/>
  public async Task NextTrack() {
    Logging.Debug($"Playing next song...");
    if (_currentPlaybackIndex + 1 < _playbackQueue.Count) {
      await ChangeTrack(_currentPlaybackIndex + 1);
      if (PlaybackState != PlaybackState.Playing && !_isPlayPending) {
        await Play();
      }
    } else {
      Logging.Debug($"Reached the end of the queue, stopping playback.");
      Stop();
    }
  }

  private void CancelPreload() {
    _preloadingSong = null;
    _preloadingTask = null;
    _preloadedPlaybackService.Replace(null!);
    _preloadCts.Replace(new CancellationTokenSource());
  }

  private Task<IPlaybackService> CreatePlaybackServiceAsync(Song song, SongStream songStream, CancellationToken token) {
    var codec = songStream.Codec;
    if (codec.StartsWith("mp4a")) {
      codec = "m4a";
    }

    return Task.Run(() => {
      try {
        token.ThrowIfCancellationRequested();
        return songStream.LoudnessDb.HasValue
          ? _audioService.MakePlaybackService(song, songStream.Stream, codec, songStream.LoudnessDb.Value, token)
          : _audioService.MakePlaybackService(song, songStream.Stream, codec, token);
      } catch {
        songStream.Stream.Dispose();
        throw;
      }
    }, token);
  }

  private void EnsurePreload() {
    var nextSongIndex = _currentPlaybackIndex + 1;
    if (nextSongIndex >= _playbackQueue.Count) {
      CancelPreload();
      return;
    }

    var nextSong = _playbackQueue[nextSongIndex];
    if (_preloadingSong == nextSong &&
        (_preloadedPlaybackService.Resource != null || (_preloadingTask is { } existingTask && !existingTask.IsFaulted && !existingTask.IsCanceled))) {
      return;
    }

    _preloadedPlaybackService.Replace(null!);
    _preloadingSong = nextSong;
    var token = _preloadCts.Replace(new CancellationTokenSource()).Token;
    _preloadingTask = PreloadTrackImpl(nextSong, token);
  }

  private async Task<IPlaybackService> PreloadTrackImpl(Song song, CancellationToken token) {
    try {
      Logging.Debug($"Preloading stream for {song.Title} ({song.Id})...");
      var songStream = await _streamingClient.GetSongStreamAsync(song.Id, token);
      if (token.IsCancellationRequested) {
        songStream.Stream.Dispose();
        token.ThrowIfCancellationRequested();
      }

      var playback = await CreatePlaybackServiceAsync(song, songStream, token);
      if (token.IsCancellationRequested) {
        playback.Dispose();
        token.ThrowIfCancellationRequested();
      }
      _preloadedPlaybackService.Replace(playback);
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
    ArgumentOutOfRangeException.ThrowIfLessThan(position, TimeSpan.Zero);
    if (_playbackService.Resource is not { } playback) {
      if (CurrentSong is { } && position <= Duration) {
        _pendingRestorePosition = position;
        InvokeAppEvent(PositionChanged, position);
      } else {
        Logging.Debug("Can't seek, no current song (no playback resource)");
      }
      return;
    }
    ArgumentOutOfRangeException.ThrowIfGreaterThan(position, Duration);
    playback.Seek(position);
  }

  /// <inheritdoc/>
  public void RestoreSessionState(SessionState state) {
    ArgumentNullException.ThrowIfNull(state);
    Stop();
    _playbackCts.Replace(new CancellationTokenSource());
    _playbackService.Replace(null!);
    CancelPreload();
    _playbackQueue.Clear();
    _pendingRestorePosition = null;

    Volume = Math.Clamp(state.Volume, 0f, 2.0f);

    if (state.Queue is { Count: > 0 }) {
      _playbackQueue.AddRange(state.Queue);
      _currentPlaybackIndex = Math.Clamp(state.CurrentPlaybackIndex, 0, _playbackQueue.Count - 1);
      var currentSong = GetCurrentSong()!;
      InvokeAppEvent(QueueChanged);
      InvokeAppEvent(SongChanged, currentSong);

      if (state.CurrentPositionSeconds > 0) {
        var restoredPos = TimeSpan.FromSeconds(state.CurrentPositionSeconds);
        if (currentSong.Duration <= TimeSpan.Zero || restoredPos <= currentSong.Duration) {
          _pendingRestorePosition = restoredPos;
          InvokeAppEvent(PositionChanged, restoredPos);
        }
      }
      EnsurePreload();
    } else {
      _currentPlaybackIndex = 0;
      InvokeAppEvent(QueueChanged);
      InvokeAppEvent(SongChanged, null);
    }
  }

  /// <inheritdoc/>
  public SessionState GetSessionState() {
    return new SessionState(
      _playbackQueue.ToList(),
      _currentPlaybackIndex,
      CurrentTime.TotalSeconds,
      Volume);
  }

  /// <inheritdoc/>
  public void SeekForward(TimeSpan duration) {
    var targetPosition = CurrentTime + duration;
    SeekTo(targetPosition > Duration ? Duration : targetPosition);
  }

  /// <inheritdoc/>
  public async Task SetAudioServiceAsync(IAudioService audioService) {
    var wasPlaying = PlaybackState == PlaybackState.Playing;
    var wasPaused = PlaybackState == PlaybackState.Paused;
    var currentTime = CurrentTime;
    var previousVolume = _audioService.Volume;

    Stop();
    _playbackService.Replace(null!);
    _preloadedPlaybackService.Replace(null!);
    _preloadingSong = null;
    _preloadingTask = null;

    var oldAudioService = _audioService;
    _audioService = audioService;
    _audioService.Volume = previousVolume;
    oldAudioService?.Dispose();

    if (_playbackQueue.Count > 0 && (wasPlaying || wasPaused)) {
      await Play();
      SeekTo(currentTime);
      if (wasPaused) {
        Pause();
      }
    }
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
    try {
      await NextTrack();
    } catch (Exception ex) {
      Logging.Error($"Failed to transition to next track on song end: {ex}");
    }
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

  private Song? GetNextSong() {
    var nextIndex = _currentPlaybackIndex + 1;
    if (nextIndex >= _playbackQueue.Count) {
      return null;
    }

    return _playbackQueue[nextIndex];
  }

  private static bool IsForbiddenHttpError(Exception ex) {
    for (var current = ex; current is not null; current = current.InnerException) {
      if (current is AggregateException aggregateException) {
        return aggregateException.Flatten().InnerExceptions.Any(IsForbiddenHttpError);
      }
      if (current is HttpRequestException httpEx &&
          (httpEx.StatusCode == HttpStatusCode.Forbidden || httpEx.Message.Contains("403"))) {
        return true;
      }
    }

    return false;
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
    if (_mainWindow.App is { } app) {
      app.Invoke(() => eventHandler?.Invoke(this, EventArgs.Empty));
    } else {
      eventHandler?.Invoke(this, EventArgs.Empty);
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
  /// <param name="args">The arguments to pass to the event handler.</param>
  private void InvokeAppEvent<T>(EventHandler<T>? eventHandler, T args) {
    if (_mainWindow.App is { } app) {
      app.Invoke(() => eventHandler?.Invoke(this, args));
    } else {
      eventHandler?.Invoke(this, args);
    }
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
