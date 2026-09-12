using Sharpcaster.Models.Media;
using Smoc.Services.Cast;
using Smoc.Streaming;
using Smoc.Services.Audio;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Gui.App;

namespace Smoc.Services.Audio.Cast;

/// <summary>
/// Playback service for a single song on a Google Cast device.
/// </summary>
public sealed class CastPlaybackService : IPlaybackService {
  private readonly IChromecastClient _client;
  private readonly Song _song;
  private readonly Stream _stream;
  private readonly string _url;
  private readonly IStreamingProxyService _proxyService;
  private readonly string _contentType;
  private readonly object _commandLock = new();
  private readonly CancellationTokenSource _disposeCts = new();
  private Task _lastCommandTask = Task.CompletedTask;
  private PlaybackState _state = PlaybackState.Stopped;
  private TimeSpan _currentTime = TimeSpan.Zero;
  private TimeSpan _duration = TimeSpan.Zero;

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
  public CastPlaybackService(
    IChromecastClient client,
    Song song,
    Stream stream,
    string url,
    IStreamingProxyService proxyService,
    string contentType = "audio/mpeg") {
    _client = client;
    _song = song;
    _stream = stream;
    _url = url;
    _proxyService = proxyService;
    _contentType = contentType;

    _client.MediaStatusChanged += OnMediaStatusChanged;
  }

  /// <inheritdoc/>
  public float[] SpectrumData => [];

  /// <inheritdoc/>
  public bool IsSpectrumActive { get; set; }

  /// <inheritdoc/>
  public TimeSpan CurrentTime => _currentTime;

  /// <inheritdoc/>
  public TimeSpan Duration => _duration > TimeSpan.Zero ? _duration : _song.Duration;

  /// <inheritdoc/>
  public float Progress => Duration.TotalSeconds > 0 ? (float)(_currentTime.TotalSeconds / Duration.TotalSeconds) : 0;

  /// <inheritdoc/>
  public PlaybackState PlaybackState => _state;

  /// <inheritdoc/>
  public Song Song => _song;

  /// <summary>
  /// Enqueues an asynchronous operation to run sequentially after all previous commands complete.
  /// </summary>
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
          await prevTask.ConfigureAwait(false);
        } catch {
          // Swallow previous exception to preserve pipeline ordering
        }

        if (_disposeCts.IsCancellationRequested) {
          return;
        }

        try {
          await action().ConfigureAwait(false);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
          Logging.Error($"Error executing Cast playback command '{operationName}': {ex.Message}");
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
      if (_state == PlaybackState.Stopped) {
        await _client.LoadAsync(new Media {
          ContentUrl = _url,
          ContentType = _contentType,
          Metadata = new MusicTrackMetadata {
            Title = _song.Title,
            Artist = _song.Artist.Name
          }
        }).ConfigureAwait(false);
      } else {
        await _client.PlayAsync().ConfigureAwait(false);
      }
      UpdateState(PlaybackState.Playing);
    }, "Play");
  }

  /// <inheritdoc/>
  public void Pause() {
    EnqueueCommand(async () => {
      await _client.PauseAsync().ConfigureAwait(false);
      UpdateState(PlaybackState.Paused);
    }, "Pause");
  }

  /// <inheritdoc/>
  public void Stop() {
    EnqueueCommand(async () => {
      await _client.StopAsync().ConfigureAwait(false);
      UpdateState(PlaybackState.Stopped);
    }, "Stop");
  }

  /// <inheritdoc/>
  public void Seek(TimeSpan position) {
    EnqueueCommand(async () => {
      await _client.SeekAsync(position.TotalSeconds).ConfigureAwait(false);
    }, "Seek");
  }

  private void UpdateState(PlaybackState newState) {
    if (_state != newState) {
      _state = newState;
      PlaybackStateChanged?.Invoke(this, _state);
    }
  }

  private void OnMediaStatusChanged(object? sender, MediaStatus e) {
    _currentTime = TimeSpan.FromSeconds(e.CurrentTime);
    if (e.Media?.Duration != null) {
      _duration = TimeSpan.FromSeconds(e.Media.Duration.Value);
    }

    PositionChanged?.Invoke(this, _currentTime);

    var playerState = e.PlayerState.ToString();
    var newState = playerState switch {
      "Playing" => PlaybackState.Playing,
      "Paused" => PlaybackState.Paused,
      "Buffering" => PlaybackState.Playing,
      _ => PlaybackState.Stopped
    };

    if (e.IdleReason?.ToString() == "FINISHED") {
      SongEnded?.Invoke(this, EventArgs.Empty);
    }

    UpdateState(newState);
  }

  /// <inheritdoc/>
  public void Dispose() {
    lock (_commandLock) {
      _disposeCts.Cancel();
    }

    try {
      _lastCommandTask.Wait(TimeSpan.FromSeconds(2));
    } catch {
      // Ignore cancellation or timeouts during shutdown
    }

    _client.MediaStatusChanged -= OnMediaStatusChanged;
    _stream.Dispose();
    _proxyService.StopProxy(_url);
    _disposeCts.Dispose();
  }
}
