using Terminal.Gui.App;
using Sharpcaster.Models;
using Sharpcaster.Models.Media;
using Smoc.Services.Cast;
using Smoc.Streaming;
using Smoc.Services.Audio;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Smoc.Services.Audio.Cast;

/// <summary>
/// Playback service that streams audio to a Google Cast device.
/// </summary>
public sealed class CastPlaybackService : IPlaybackService {
  private readonly IChromecastClient _client;
  private readonly Song _song;
  private readonly Stream _stream;
  private readonly string _url;
  private readonly IStreamingProxyService _proxyService;
  private readonly string _contentType;
  private readonly Func<CancellationToken, Task>? _ensureConnection;
  private readonly object _stateLock = new();
  private readonly object _progressLock = new();
  private readonly object _commandLock = new();
  private readonly CancellationTokenSource _disposeCts = new();
  private Task _lastCommandTask = Task.CompletedTask;
  private PlaybackState _state = PlaybackState.Stopped;
  private bool _hasStarted;

  private TimeSpan _statusPosition = TimeSpan.Zero;
  private long _startTimestamp;
  private TimeSpan _duration = TimeSpan.Zero;
  private CancellationTokenSource? _progressCts;
  private Task? _progressLoopTask;

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
  /// <param name="contentType">The content type of the stream.</param>
  /// <param name="ensureConnection">Optional delegate to ensure device connectivity and receiver application launch before playing.</param>
  public CastPlaybackService(
    IChromecastClient client,
    Song song,
    Stream stream,
    string url,
    IStreamingProxyService proxyService,
    string contentType = "audio/mpeg",
    Func<CancellationToken, Task>? ensureConnection = null) {
    _client = client;
    _song = song;
    _stream = stream;
    _url = url;
    _proxyService = proxyService;
    _contentType = contentType;
    _ensureConnection = ensureConnection;

    _client.MediaStatusChanged += OnMediaStatusChanged;
  }

  /// <summary>
  /// Initializes a new instance of the <see cref="CastPlaybackService"/> class with a parameterless connection delegate.
  /// </summary>
  /// <param name="client">The Cast client.</param>
  /// <param name="song">The song to play.</param>
  /// <param name="stream">The stream of the song.</param>
  /// <param name="url">The URL where the stream is proxied.</param>
  /// <param name="proxyService">The proxy service.</param>
  /// <param name="contentType">The content type of the stream.</param>
  /// <param name="ensureConnection">Delegate to ensure device connectivity.</param>
  public CastPlaybackService(
    IChromecastClient client,
    Song song,
    Stream stream,
    string url,
    IStreamingProxyService proxyService,
    string contentType,
    Func<Task> ensureConnection)
    : this(client, song, stream, url, proxyService, contentType, _ => ensureConnection()) {
  }

  /// <inheritdoc/>
  public float[] SpectrumData => [];

  /// <inheritdoc/>
  public bool IsSpectrumActive { get; set; }

  /// <inheritdoc/>
  public TimeSpan CurrentTime {
    get {
      lock (_progressLock) {
        if (_state == PlaybackState.Playing && _startTimestamp > 0) {
          var elapsedSeconds = (Stopwatch.GetTimestamp() - _startTimestamp) / (double)Stopwatch.Frequency;
          var calculated = _statusPosition + TimeSpan.FromSeconds(elapsedSeconds);
          return calculated < Duration ? calculated : Duration;
        }
        return _statusPosition;
      }
    }
  }

  /// <inheritdoc/>
  public TimeSpan Duration => _duration > TimeSpan.Zero ? _duration : _song.Duration;

  /// <inheritdoc/>
  public float Progress => Duration.TotalSeconds > 0
    ? (float)(CurrentTime.TotalSeconds / Duration.TotalSeconds)
    : 0;

  /// <inheritdoc/>
  public PlaybackState PlaybackState => _state;

  /// <inheritdoc/>
  public Song Song => _song;

  /// <inheritdoc/>
  public PlaybackState State {
    get {
      lock (_stateLock) {
        return _state;
      }
    }
  }

  private void EnqueueCommand(Func<Task> action, string operationName) {
    lock (_commandLock) {
      if (_disposeCts.IsCancellationRequested) {
        return;
      }

      _lastCommandTask = _lastCommandTask.ContinueWith(async prevTask => {
        if (_disposeCts.IsCancellationRequested) {
          return;
        }

        try {
          await prevTask;
        } catch {
          // Swallow previous exception to preserve pipeline ordering
        }

        if (_disposeCts.IsCancellationRequested) {
          return;
        }

        try {
          await action();
        } catch (Exception ex) when (ex is not OperationCanceledException) {
          Logging.Error($"Error executing Cast playback command \"{operationName}\": {ex.Message}");
        }
      }, TaskScheduler.Default).Unwrap();
    }
  }

  /// <summary>
  /// Waits for all pending enqueued commands to complete.
  /// </summary>
  /// <returns>A task representing the completion of all pending commands.</returns>
  internal Task WaitForPendingCommandsAsync() {
    lock (_commandLock) {
      return _lastCommandTask;
    }
  }

  /// <inheritdoc/>
  public void Play() {
    EnqueueCommand(async () => {
      _hasStarted = true;
      if (_state == PlaybackState.Playing) {
        return;
      }

      if (_ensureConnection != null) {
        await _ensureConnection(_disposeCts.Token);
      }

      if (_state == PlaybackState.Stopped) {
        await _client.LoadAsync(new Media {
          ContentUrl = _url,
          ContentType = _contentType,
          Metadata = new MusicTrackMetadata {
            Title = _song.Title,
            Artist = _song.Artist.Name
          }
        }, _disposeCts.Token);
      } else {
        await _client.PlayAsync(_disposeCts.Token);
      }
      UpdateState(PlaybackState.Playing);
      StartProgressTracking();
    }, "Play");
  }

  /// <inheritdoc/>
  public void Pause() {
    EnqueueCommand(async () => {
      if (_state == PlaybackState.Paused || _state == PlaybackState.Stopped) {
        return;
      }

      await _client.PauseAsync(_disposeCts.Token);
      StopProgressTracking(resetPosition: false);
      UpdateState(PlaybackState.Paused);
      PositionChanged?.Invoke(this, CurrentTime);
    }, "Pause");
  }

  /// <inheritdoc/>
  public void Stop() {
    EnqueueCommand(async () => {
      if (_state == PlaybackState.Stopped) {
        return;
      }

      await _client.StopAsync(_disposeCts.Token);
      StopProgressTracking(resetPosition: true);
      UpdateState(PlaybackState.Stopped);
      PositionChanged?.Invoke(this, TimeSpan.Zero);
    }, "Stop");
  }

  /// <inheritdoc/>
  public void Seek(TimeSpan position) {
    EnqueueCommand(async () => {
      await _client.SeekAsync(position.TotalSeconds, _disposeCts.Token);
      lock (_progressLock) {
        _statusPosition = position;
        if (_state == PlaybackState.Playing) {
          _startTimestamp = Stopwatch.GetTimestamp();
        }
      }
      PositionChanged?.Invoke(this, position);
    }, "Seek");
  }

  private void StartProgressTracking() {
    lock (_progressLock) {
      _startTimestamp = Stopwatch.GetTimestamp();
      if (_progressLoopTask != null && !_progressLoopTask.IsCompleted) {
        return;
      }

      _progressCts?.Cancel();
      _progressCts?.Dispose();
      _progressCts = new CancellationTokenSource();
      var token = _progressCts.Token;

      _progressLoopTask = Task.Run(async () => {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        int pollCounter = 0;
        try {
          while (!token.IsCancellationRequested && await timer.WaitForNextTickAsync(token)) {
            if (_state == PlaybackState.Playing) {
              PositionChanged?.Invoke(this, CurrentTime);

              if (++pollCounter >= 20) {
                pollCounter = 0;
                _ = PollMediaStatusAsync(token);
              }
            }
          }
        } catch (OperationCanceledException) {
          // Normal cancellation
        }
      }, CancellationToken.None);
    }
  }

  private void StopProgressTracking(bool resetPosition) {
    lock (_progressLock) {
      _progressCts?.Cancel();
      _progressCts?.Dispose();
      _progressCts = null;
      _progressLoopTask = null;

      if (_state == PlaybackState.Playing && _startTimestamp > 0) {
        var elapsedSeconds = (Stopwatch.GetTimestamp() - _startTimestamp) / (double)Stopwatch.Frequency;
        _statusPosition += TimeSpan.FromSeconds(elapsedSeconds);
      }
      _startTimestamp = 0;

      if (resetPosition) {
        _statusPosition = TimeSpan.Zero;
      }
    }
  }

  private async Task PollMediaStatusAsync(CancellationToken cancellationToken) {
    try {
      var status = await _client.GetMediaStatusAsync(cancellationToken);
      if (status != null) {
        OnMediaStatusChanged(this, status);
      }
    } catch {
      // Ignore polling errors
    }
  }

  private void OnMediaStatusChanged(object? sender, MediaStatus e) {
    if (!_hasStarted) {
      return;
    }

    if (e.Media?.ContentUrl != null &&
        !string.Equals(e.Media.ContentUrl, _url, StringComparison.OrdinalIgnoreCase)) {
      return;
    }

    lock (_progressLock) {
      if (e.CurrentTime > 0) {
        _statusPosition = TimeSpan.FromSeconds(e.CurrentTime);
        if (_state == PlaybackState.Playing) {
          _startTimestamp = Stopwatch.GetTimestamp();
        }
      }
      if (e.Media?.Duration != null && e.Media.Duration.Value > 0) {
        _duration = TimeSpan.FromSeconds(e.Media.Duration.Value);
      }
    }

    PositionChanged?.Invoke(this, CurrentTime);

    var wasPlaying = false;
    lock (_stateLock) {
      wasPlaying = _state == PlaybackState.Playing;
    }

    PlaybackState? newState = e.PlayerState switch {
      PlayerStateType.Playing => PlaybackState.Playing,
      PlayerStateType.Paused => PlaybackState.Paused,
      PlayerStateType.Idle => PlaybackState.Stopped,
      PlayerStateType.Buffering => PlaybackState.Playing,
      _ => null
    };

    if (newState.HasValue) {
      UpdateState(newState.Value);
    }

    if (wasPlaying && string.Equals(e.IdleReason, "FINISHED", StringComparison.OrdinalIgnoreCase)) {
      SongEnded?.Invoke(this, EventArgs.Empty);
    }
  }

  private void UpdateState(PlaybackState newState) {
    bool changed;
    lock (_stateLock) {
      changed = _state != newState;
      _state = newState;
    }
    if (changed) {
      PlaybackStateChanged?.Invoke(this, newState);
    }
  }

  /// <inheritdoc/>
  public void Dispose() {
    _disposeCts.Cancel();
    StopProgressTracking(resetPosition: false);
    _client.MediaStatusChanged -= OnMediaStatusChanged;

    try {
      _lastCommandTask.Wait(TimeSpan.FromSeconds(2));
    } catch {
      // Ignore timeout or cancellation on dispose
    }

    _proxyService.StopProxy(_url);
    _stream.Dispose();
    _disposeCts.Dispose();
  }
}
