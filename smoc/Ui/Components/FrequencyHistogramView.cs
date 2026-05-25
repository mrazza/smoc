using Terminal.Gui.Time;

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

    for (int i = 0; i < totalBars; i++) {
      int col = i * 2;
      float amp = _amplitudes[i];
      float totalLevels = amp * height * 8;

      for (int r = 0; r < height; r++) {
        int cellLevel = (int)totalLevels - (r * 8);
        int row = height - 1 - r; // Draw from bottom up

        // Select the color for this vertical level segment
        int colorIndex = (int)Math.Clamp((double)r / Math.Max(1, height) * _gradientColors.Length, 0, _gradientColors.Length - 1);
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
            _ => ' '
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
    if (_timerToken != null || App == null) {
      return;
    }

    _timerToken = App.AddTimeout(TimeSpan.FromMilliseconds(100), () => {
      UpdateVisualization();
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
      int minBin = Math.Min(1, spectrum.Length - 1);
      // Discard DC offset (bin 0) and map up to 20 kHz (approx 90.7% of Nyquist at 44.1 kHz sample rate)
      int maxBin = Math.Clamp((int)(spectrum.Length * 0.907f), minBin + 1, spectrum.Length);

      // Group spectrum bins logarithmically into columns
      for (int i = 0; i < totalBars; i++) {
        double lowFrac = Math.Pow((double)i / totalBars, 1.5);
        double highFrac = Math.Pow((double)(i + 1) / totalBars, 1.5);

        int startBin = minBin + (int)(lowFrac * (maxBin - minBin));
        int endBin = minBin + (int)(highFrac * (maxBin - minBin));
        if (endBin <= startBin) {
          endBin = startBin + 1;
        }

        float sum = 0f;
        int count = 0;
        for (int bin = startBin; bin < endBin && bin < spectrum.Length; bin++) {
          sum += spectrum[bin];
          count++;
        }
        float amp = count > 0 ? sum / count : 0f;

        // Apply a psychoacoustic weighting filter (sine window) to roll off subsonic DC offset on the left and ultrasonic noise on the right
        double fraction = (double)i / totalBars;
        double weight = Math.Sin((0.05 + 0.90 * fraction) * Math.PI);
        
        // Scale with a safer 4.5f coefficient to completely eliminate excessive pegging
        float target = amp * 4.5f * (float)weight;
        target = Math.Clamp(target, 0.0f, 1.0f);

        // Attack & decay smoothing
        if (target > _amplitudes[i]) {
          _amplitudes[i] += (target - _amplitudes[i]) * 0.7f; // Quick attack
        } else {
          _amplitudes[i] += (target - _amplitudes[i]) * 0.25f; // Gradual decay
        }
      }
    } else if (isPlaying) {
      // Procedural fallback animation (e.g. for unit tests or streams without FFT)
      double t = _timeProvider.Now.ToUniversalTime().Subtract(DateTime.UnixEpoch).TotalMilliseconds;
      for (int i = 0; i < totalBars; i++) {
        double tSeconds = t / 1000.0;
        double fraction = (double)i / totalBars;

        double speed = 3.0 + fraction * 10.0;
        double wave1 = Math.Sin(tSeconds * speed + i * 0.8);
        double wave2 = Math.Cos(tSeconds * (speed * 0.5) - i * 0.4);
        double wave3 = Math.Sin(tSeconds * (speed * 0.2) + i * 1.5);
        double blended = 0.5 * wave1 + 0.3 * wave2 + 0.2 * wave3;

        double freqBias = Math.Pow(1.0 - fraction, 0.3);
        float target = (float)((0.1 + 0.9 * Math.Abs(blended)) * freqBias);
        target = Math.Clamp(target, 0.0f, 1.0f);

        if (target > _amplitudes[i]) {
          _amplitudes[i] += (target - _amplitudes[i]) * 0.6f;
        } else {
          _amplitudes[i] += (target - _amplitudes[i]) * 0.3f;
        }
      }
    } else {
      // Natural visual decay fallback to 0 when paused or stopped
      bool stillDecaying = false;
      for (int i = 0; i < totalBars; i++) {
        _amplitudes[i] *= 0.75f;
        if (_amplitudes[i] < 0.01f) {
          _amplitudes[i] = 0f;
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
