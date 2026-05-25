namespace smoc.Tests.Ui.Components;

using Moq;
using smoc.Tests.TestInfra;
using Smoc.Services;
using Smoc.Ui.Components;
using Terminal.Gui.Views;
using View = Terminal.Gui.ViewBase.View;

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

  private FrequencyHistogramView NewVisualizer(Func<double>? timeSource = null) => new FrequencyHistogramView(_mockPlaybackQueue.Object, timeSource);

  private AppTestHelper NewVisualizerContext(int width = 40, int height = 10, Func<double>? timeSource = null) {
    var view = NewVisualizer(timeSource);
    view.Visible = false; // Start invisible so that OnVisibleChanged fires when context is active
    view.Width = Terminal.Gui.ViewBase.Dim.Fill();
    view.Height = Terminal.Gui.ViewBase.Dim.Fill();
    var context = NewContext(width, height).AddAndLayout(view);
    
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
    using var context = NewVisualizerContext(timeSource: () => 3500.0);
    
    // Trigger two update ticks to transition attack/decay interpolation towards target values
    context.AdvanceTime(TimeSpan.FromMilliseconds(100));
    context.AdvanceTime(TimeSpan.FromMilliseconds(100));

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
    mockFrequencies[1] = 0.8f;  // Bass peak
    mockFrequencies[2] = 0.9f;
    mockFrequencies[10] = 0.5f; // Mid peak
    mockFrequencies[28] = 0.3f; // Treble peak
    _mockPlaybackQueue.SetupGet(q => q.SpectrumData).Returns(mockFrequencies);

    using var context = NewVisualizerContext();

    context.AdvanceTime(TimeSpan.FromMilliseconds(100));
    context.AdvanceTime(TimeSpan.FromMilliseconds(100));

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

    using var context = NewVisualizerContext(timeSource: () => 2000.0);

    // Pump initial values
    context.AdvanceTime(TimeSpan.FromMilliseconds(100));
    context.AdvanceTime(TimeSpan.FromMilliseconds(100));

    // 2. Pause playback
    _mockPlaybackQueue.SetupGet(q => q.PlaybackState).Returns(PlaybackState.Paused);

    // 3. Pump updates to run the decay logic
    context.AdvanceTime(TimeSpan.FromMilliseconds(100));
    context.AdvanceTime(TimeSpan.FromMilliseconds(100));

    _screenshotDiffer.AssertEqualsGolden(context);
  }
}
