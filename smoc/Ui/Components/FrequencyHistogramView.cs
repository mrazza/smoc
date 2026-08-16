using Terminal.Gui.Time;
using Smoc.Configuration;

namespace Smoc.Ui.Components;

using System;
using System.Drawing;
using Smoc.Services;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Color = Terminal.Gui.Drawing.Color;
using Attribute = Terminal.Gui.Drawing.Attribute;

/// <summary>
/// A terminal view that renders a beautiful dynamic frequency histogram/equalizer
/// representing the frequencies of the currently playing track.
/// </summary>
public sealed class FrequencyHistogramView : View {
  private static readonly Color[] _gradientColors = [
    new Color(0, 220, 100),   // Green
    new Color(100, 220, 0),   // Lime
    new Color(220, 220, 0),   // Yellow
    new Color(255, 150, 0),   // Orange
    new Color(255, 50, 50)    // Red
  ];

  private readonly IPlaybackQueueService _playbackQueueService;
  private readonly ITimeProvider _timeProvider;
  private float[] _amplitudes = [];
  private object? _timerToken;
  private int _timerFps;

  /// <summary>
  /// Initializes a new instance of the <see cref="FrequencyHistogramView"/> class.
  /// </summary>
  /// <param name="playbackQueueService">The playback queue service for retrieving spectrum data.</param>
  /// <param name="timeProvider">An optional custom time provider for deterministic testing.</param>
  public FrequencyHistogramView(IPlaybackQueueService playbackQueueService, ITimeProvider? timeProvider = null) {
    _playbackQueueService = playbackQueueService;
    _timeProvider = timeProvider ?? new SystemTimeProvider();
    CanFocus = false;

    _playbackQueueService.PlaybackStateChanged += OnPlaybackStateChanged;
  }

  /// <inheritdoc/>
  protected override void OnVisibleChanged() {
    base.OnVisibleChanged();
    UpdateTimerState();
  }

  /// <inheritdoc/>
  protected override bool OnDrawingContent(DrawContext? context) {
    base.OnDrawingContent(context);

    Rectangle contentArea = Viewport;
    if (contentArea.Width <= 0 || contentArea.Height <= 0) {
      return true;
    }

    // Space-separated bars (1 character for bar, 1 character space gap)
    int totalBars = (contentArea.Width + 1) / 2;
    if (_amplitudes.Length != totalBars) {
      Array.Resize(ref _amplitudes, totalBars);
    }

    UpdateTimerState();

    int height = contentArea.Height;
    var currentAttr = GetCurrentAttribute();

    for (int currBar = 0; currBar < totalBars; currBar++) {
      int col = currBar * 2;
      float amp = _amplitudes[currBar];
      float totalLevels = amp * height * 8;

      for (int rowIndex = 0; rowIndex < height; rowIndex++) {
        int cellLevel = (int)totalLevels - (rowIndex * 8);
        int row = height - 1 - rowIndex; // Draw from bottom up

        // Select the color for this vertical level segment
        int colorIndex = (int)Math.Clamp((double)rowIndex / Math.Max(1, height) * _gradientColors.Length, 0, _gradientColors.Length - 1);
        var barColor = _gradientColors[colorIndex];
        SetAttribute(new Attribute(barColor, currentAttr.Background));

        if (cellLevel <= 0) {
          if (Move(col, row)) {
            AddRune(' ');
          }
        } else if (cellLevel >= 8) {
          if (Move(col, row)) {
            AddRune('█');
          }
        } else {
          char blockChar = cellLevel switch {
            1 => '\u2581',
            2 => '▂',
            3 => '▃',
            4 => '▄',
            5 => '▅',
            6 => '▆',
            7 => '▇',
            _ => 'X'
          };
          if (Move(col, row)) {
            AddRune(blockChar);
          }
        }

        if (col + 1 < contentArea.Width) {
          SetAttribute(currentAttr);
          if (Move(col + 1, row)) {
            AddRune(' ');
          }
        }
      }
    }

    // Restore the original terminal style
    SetAttribute(currentAttr);
    return true;
  }

  /// <inheritdoc/>
  protected override void Dispose(bool disposing) {
    _playbackQueueService.PlaybackStateChanged -= OnPlaybackStateChanged;
    StopTimer();
    base.Dispose(disposing);
  }

  private void OnPlaybackStateChanged(object? sender, PlaybackState state) {
    UpdateTimerState();
  }

  private void UpdateTimerState() {
    bool isDecaying = false;
    if (_amplitudes != null) {
      for (int i = 0; i < _amplitudes.Length; i++) {
        if (_amplitudes[i] > 0f) {
          isDecaying = true;
          break;
        }
      }
    }

    bool shouldRun = Visible && (_playbackQueueService.PlaybackState == PlaybackState.Playing || isDecaying);
    if (shouldRun) {
      StartTimer();
    } else {
      StopTimer();
    }
  }

  private void StartTimer() {
    if (_timerToken is not null || App == null) {
      return;
    }

    _timerFps = Math.Clamp(SmocConfiguration.Defaults.VisualizerFps, 1, 60);
    double intervalMs = 1000.0 / _timerFps;

    _timerToken = App.AddTimeout(TimeSpan.FromMilliseconds(intervalMs), () => {
      UpdateVisualization();

      int desiredFps = Math.Clamp(SmocConfiguration.Defaults.VisualizerFps, 1, 60);

      if (_timerToken == null) {
        // If something cleared the token during processing, we need to cancel this timer to avoid orphaned timers running indefinitely.
        return false;
      }

      if (desiredFps != _timerFps) {
        // Clear the token to allow a new timer to be created with the updated FPS
        _timerToken = null;
        StartTimer();
        return false;
      }

      return true;
    });
  }

  private void StopTimer() {
    if (_timerToken != null && App != null) {
      App.RemoveTimeout(_timerToken);
      _timerToken = null;
    }
  }

  private void UpdateVisualization() {
    if (App == null || !Visible) {
      StopTimer();
      return;
    }

    bool isPlaying = _playbackQueueService.PlaybackState == PlaybackState.Playing;
    int totalBars = _amplitudes.Length;

    if (totalBars == 0 && Viewport.Width > 0) {
      totalBars = (Viewport.Width + 1) / 2;
      Array.Resize(ref _amplitudes, totalBars);
    }

    if (totalBars == 0) {
      SetNeedsDraw();
      return;
    }

    float[] spectrum = _playbackQueueService.SpectrumData;

    // Use atomic / thread-safe local reference for the spectrum data
    if (isPlaying && spectrum != null && spectrum.Length > 0) {
      // Group spectrum bins logarithmically into columns
      for (int currBar = 0; currBar < totalBars; currBar++) {
        // Invert the logarithmic mapping so wider spectrum ranges occur at higher frequencies
        double startFrac = (double)currBar / totalBars;
        double endFrac = (double)(currBar + 1) / totalBars;
        double startLogIndex = 1.0 - Math.Log10(1 + 9 * (1.0 - startFrac));
        double endLogIndex = 1.0 - Math.Log10(1 + 9 * (1.0 - endFrac));

        int startSpectrumIndex = (int)Math.Round(startLogIndex * (spectrum.Length - 1));
        int endSpectrumIndex = (int)Math.Round(endLogIndex * (spectrum.Length - 1)) - 1;

        if (startSpectrumIndex == endSpectrumIndex) {
          endSpectrumIndex = startSpectrumIndex + 1;
        }

        double magnitude = 0;
        for (int spectrumIndex = startSpectrumIndex; spectrumIndex < endSpectrumIndex; spectrumIndex++) {
          magnitude = Math.Max(magnitude, spectrum[spectrumIndex]);
        }
        magnitude = Math.Log(magnitude + 1) / Math.Log(256.0);

        // Attack & decay smoothing
        if (magnitude > _amplitudes[currBar]) {
          _amplitudes[currBar] += (float)(magnitude - _amplitudes[currBar]) * 0.9f; // Quick attack
        } else {
          _amplitudes[currBar] += (float)(magnitude - _amplitudes[currBar]) * 0.4f; // Gradual decay
        }
      }
    } else if (isPlaying) {
      // Procedural fallback animation (e.g. for unit tests or streams without FFT)
      double time = _timeProvider.Now.ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalMilliseconds;
      for (int currBar = 0; currBar < totalBars; currBar++) {
        double tSeconds = time / 1000.0;
        double fraction = (double)currBar / totalBars;

        double speed = 3.0 + fraction * 10.0;
        double wave1 = Math.Sin(tSeconds * speed + currBar * 0.8);
        double wave2 = Math.Cos(tSeconds * (speed * 0.5) - currBar * 0.4);
        double wave3 = Math.Sin(tSeconds * (speed * 0.2) + currBar * 1.5);
        double blended = 0.5 * wave1 + 0.3 * wave2 + 0.2 * wave3;

        double freqBias = Math.Pow(1.0 - fraction, 0.3);
        float target = (float)((0.1 + 0.9 * Math.Abs(blended)) * freqBias);
        target = Math.Clamp(target, 0.0f, 1.0f);

        if (target > _amplitudes[currBar]) {
          _amplitudes[currBar] += (target - _amplitudes[currBar]) * 0.6f;
        } else {
          _amplitudes[currBar] += (target - _amplitudes[currBar]) * 0.3f;
        }
      }
    } else {
      // Natural visual decay fallback to 0 when paused or stopped
      bool stillDecaying = false;
      for (int currBar = 0; currBar < totalBars; currBar++) {
        _amplitudes[currBar] *= 0.75f;
        if (_amplitudes[currBar] < 0.01f) {
          _amplitudes[currBar] = 0f;
        } else {
          stillDecaying = true;
        }
      }

      if (!stillDecaying) {
        StopTimer();
      }
    }

    SetNeedsDraw();
  }
}
