namespace smoc.Tests.Ui.Components;

using System;
using Moq;
using smoc.Tests.TestInfra;
using Smoc.Configuration;
using Smoc.Services;
using Smoc.Ui.Components;
using Terminal.Gui.Views;
using Terminal.Gui.Time;

/// <summary>
/// Unit tests for <see cref="FrequencyHistogramView"/> verifying rendering, mapping, and state changes.
/// </summary>
public class FrequencyHistogramViewTest {
  private readonly Mock<IPlaybackQueueService> _mockPlaybackQueue;
  private readonly ScreenshotDiffer _screenshotDiffer;

  /// <summary>
  /// Initializes a new instance of the <see cref="FrequencyHistogramViewTest"/> class.
  /// </summary>
  /// <param name="output">The test output helper provided by xUnit.</param>
  public FrequencyHistogramViewTest(ITestOutputHelper output) {
    _mockPlaybackQueue = new Mock<IPlaybackQueueService>();
    _screenshotDiffer = new ScreenshotDiffer(output);
  }

  private static AppTestHelper NewContext(int width = 40, int height = 10) => With.A<Runnable>(width, height, TestDriver.ANSI.ToString());

  private FrequencyHistogramView NewVisualizer(ITimeProvider timeProvider) => new FrequencyHistogramView(_mockPlaybackQueue.Object, timeProvider);

  private AppTestHelper NewVisualizerContext(int width = 40, int height = 10, int? startEpochOffsetMs = null) {
    var context = NewContext(width, height);
    if (startEpochOffsetMs.HasValue) {
      (context.TimeProvider as VirtualTimeProvider)?.SetTime(DateTime.UnixEpoch.AddMilliseconds(startEpochOffsetMs.Value));
    }
    var view = NewVisualizer(context.TimeProvider);
    view.Visible = false; // Start invisible so that OnVisibleChanged fires when context is active
    view.Width = Terminal.Gui.ViewBase.Dim.Fill();
    view.Height = Terminal.Gui.ViewBase.Dim.Fill();
    context.AddAndLayout(view);
    
    // Set visible inside the running main loop so the timer successfully registers with App
    context.Then((_) => {
      view.Visible = true;
      context.App!.TopRunnableView!.Layout();
    });
    return context;
  }

  /// <summary>
  /// Verifies that when playback is stopped/empty, the equalizer displays completely flat/empty bars.
  /// </summary>
  [Fact]
  public void InitialState_ShowsEmptyEqualizer() {
    _mockPlaybackQueue.SetupGet(q => q.PlaybackState).Returns(PlaybackState.Stopped);
    _mockPlaybackQueue.SetupGet(q => q.SpectrumData).Returns([]);

    using var context = NewVisualizerContext();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  /// <summary>
  /// Verifies that when music is playing and no real FFT data is available, the procedural wave fallback renders beautifully and deterministically.
  /// </summary>
  [Fact]
  public void PlayingState_ProceduralFallback_RendersDeterministically() {
    _mockPlaybackQueue.SetupGet(q => q.PlaybackState).Returns(PlaybackState.Playing);
    _mockPlaybackQueue.SetupGet(q => q.SpectrumData).Returns([]);

    // Use a fixed simulated time point for frozen deterministic screenshot
    using var context = NewVisualizerContext(startEpochOffsetMs: 3500);
    
    // Trigger two update ticks to transition attack/decay interpolation towards target values
    context.AdvanceTime(TimeSpan.FromMilliseconds(101));
    context.AdvanceTime(TimeSpan.FromMilliseconds(101));

    _screenshotDiffer.AssertEqualsGolden(context);
  }

  /// <summary>
  /// Verifies that when real-time spectrum data is active, the frequency analyzer logarithmically maps the FFT bands correctly to equalizer columns.
  /// </summary>
  [Fact]
  public void PlayingState_WithRealSpectrum_MapsLogarithmically() {
    _mockPlaybackQueue.SetupGet(q => q.PlaybackState).Returns(PlaybackState.Playing);

    // Simulate 32 frequency bins from SoundFlow with distinct bass (strong) and treble peaks
    float[] mockFrequencies = new float[32];
    mockFrequencies[1] = 225;  // Bass peak
    mockFrequencies[2] = 225;  // Bass peak
    mockFrequencies[10] = 64; // Mid peak
    mockFrequencies[28] = 10; // Treble peak
    _mockPlaybackQueue.SetupGet(q => q.SpectrumData).Returns(mockFrequencies);

    using var context = NewVisualizerContext();

    context.AdvanceTime(TimeSpan.FromMilliseconds(101));

    _screenshotDiffer.AssertEqualsGolden(context);
  }

  /// <summary>
  /// Verifies that when music transitions from playing to paused/stopped, the frequencies decay smoothly towards zero.
  /// </summary>
  [Fact]
  public void StateTransition_ToPaused_DecaysFrequencies() {
    // 1. Start as playing with procedural values
    _mockPlaybackQueue.SetupGet(q => q.PlaybackState).Returns(PlaybackState.Playing);
    _mockPlaybackQueue.SetupGet(q => q.SpectrumData).Returns([]);

    using var context = NewVisualizerContext(startEpochOffsetMs: 2000);

    // Pump initial values
    context.AdvanceTime(TimeSpan.FromMilliseconds(101));
    context.AdvanceTime(TimeSpan.FromMilliseconds(101));

    // 2. Pause playback
    _mockPlaybackQueue.SetupGet(q => q.PlaybackState).Returns(PlaybackState.Paused);

    // 3. Pump updates to run the decay logic
    context.AdvanceTime(TimeSpan.FromMilliseconds(101));
    context.AdvanceTime(TimeSpan.FromMilliseconds(101));

    _screenshotDiffer.AssertEqualsGolden(context);
  }

  /// <summary>
  /// Verifies that when <see cref="SmocConfiguration.Defaults.VisualizerFps"/> changes dynamically,
  /// the timer is recreated with the correct new interval.
  /// </summary>
  [Fact]
  public void VisualizerFps_DynamicChange_RecreatesTimerWithNewInterval() {
    int originalFps = SmocConfiguration.Defaults.VisualizerFps;
    try {
      // 1. Initialize to 10 FPS (100ms interval)
      SmocConfiguration.Defaults.VisualizerFps = 10;
      _mockPlaybackQueue.SetupGet(q => q.PlaybackState).Returns(PlaybackState.Playing);
      _mockPlaybackQueue.SetupGet(q => q.SpectrumData).Returns([]);

      // Create a visualizer context inline
      using var context = NewContext(40, 10);
      (context.TimeProvider as VirtualTimeProvider)?.SetTime(DateTime.UnixEpoch.AddMilliseconds(1000.0));

      var view = NewVisualizer(context.TimeProvider);
      view.Visible = false;
      view.Width = Terminal.Gui.ViewBase.Dim.Fill();
      view.Height = Terminal.Gui.ViewBase.Dim.Fill();
      context.AddAndLayout(view);

      context.Then((_) => {
        view.Visible = true;
        context.App!.TopRunnableView!.Layout();
      });

      var amplitudesField = typeof(FrequencyHistogramView).GetField("_amplitudes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      Assert.NotNull(amplitudesField);

      // Initially, amplitudes are uninitialized/all zero
      var initialAmplitudes = (float[]?)amplitudesField.GetValue(view);
      Assert.NotNull(initialAmplitudes);
      Assert.All(initialAmplitudes, amp => Assert.Equal(0f, amp));

      // Advance by 100ms: the 10 FPS timer should trigger and procedural values should be populated
      context.AdvanceTime(TimeSpan.FromMilliseconds(100));

      var amplitudesAfter100ms = (float[]?)amplitudesField.GetValue(view);
      Assert.NotNull(amplitudesAfter100ms);
      Assert.Contains(amplitudesAfter100ms, amp => amp > 0f);

      // Now change the configuration FPS dynamically to 2 FPS (500ms interval)
      SmocConfiguration.Defaults.VisualizerFps = 2;

      // Advance by 101ms to reach the scheduled next tick (at t = 200ms)
      // When this tick fires, it detects the FPS change (10 -> 2)
      context.AdvanceTime(TimeSpan.FromMilliseconds(101));

      // Copy the amplitudes to compare later
      var amplitudesAfter200ms = (float[]?)amplitudesField.GetValue(view);
      Assert.NotNull(amplitudesAfter200ms);
      var copyAmplitudes = (float[])amplitudesAfter200ms.Clone();

      // Since the new 2 FPS timer has a 500ms interval starting at t = 200ms,
      // advancing by another 100ms (to t = 300ms) should NOT trigger any update.
      context.AdvanceTime(TimeSpan.FromMilliseconds(100));

      var amplitudesAfter300ms = (float[]?)amplitudesField.GetValue(view);
      Assert.Equal(copyAmplitudes, amplitudesAfter300ms);

      // Advancing by another 400ms (to t = 700ms, which is 500ms after the recreation at t = 200ms)
      // should trigger the new 2 FPS timer.
      context.AdvanceTime(TimeSpan.FromMilliseconds(400));

      var amplitudesAfter700ms = (float[]?)amplitudesField.GetValue(view);
      // Frequencies should have moved/updated from the wave procedural generator
      Assert.NotEqual(copyAmplitudes, amplitudesAfter700ms);
    } finally {
      // Restore configuration
      SmocConfiguration.Defaults.VisualizerFps = originalFps;
    }
  }
}
