using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using smoc.Tests.Fakes;
using smoc.Tests.TestInfra;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui;
using Smoc.Ui.Models;
using Terminal.Gui.Views;
using View = Terminal.Gui.ViewBase.View;

namespace smoc.Tests.Ui;

public class NowPlayingViewTest {
  private readonly FakeMainWindow _fakeMainWindow;
  private readonly Mock<IPlaybackQueueService> _mockPlaybackQueue;
  private readonly Mock<IStreamingClient> _mockStreamingClient;
  private readonly CommandService _commandService;
  private readonly ScreenshotDiffer _screenshotDiffer;

  public NowPlayingViewTest(ITestOutputHelper output) {
    _fakeMainWindow = new FakeMainWindow();
    _mockPlaybackQueue = new Mock<IPlaybackQueueService>();
    _commandService = new CommandService();
    _screenshotDiffer = new ScreenshotDiffer(output);
    _mockStreamingClient = new Mock<IStreamingClient>();
  }

  private static AppTestHelper NewContext(int width = 100, int height = 25) => With.A<Runnable>(width, height, TestDriver.ANSI.ToString());

  private NowPlayingView NewNowPlaying() => new NowPlayingView(_fakeMainWindow, _commandService, _mockPlaybackQueue.Object, _mockStreamingClient.Object);

  private AppTestHelper NewNowPlayingContext(int width = 100, int height = 25) => NewContext(width, height).AddAndLayout(NewNowPlaying());

  [Fact]
  public void NowPlayingCommand_ChangesModeToNowPlaying() {
    _fakeMainWindow.CurrentMode = Mode.Queue;
    using var context = NewNowPlayingContext();

    Assert.NotEqual(Mode.NowPlaying, _fakeMainWindow.CurrentMode);
    context.Then((_) => _commandService.ExecuteCommand("np"));
    Assert.Equal(Mode.NowPlaying, _fakeMainWindow.CurrentMode);
  }

  [Fact]
  public void InitialState_ShowsEmptyNowPlayingInfo() {
    using var context = NewNowPlayingContext();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void InitialState_ShowsEmptyNowPlayingInfo_Wide() {
    using var context = NewNowPlayingContext(width: 120);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void InitialState_ShowsEmptyNowPlayingInfo_Tall() {
    using var context = NewNowPlayingContext(height: 40);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void OnSongChanged_UpdatesSongDetails() {
    var song = EntityTestFactory.GenerateSong();
    EventHandler<Song?>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song?>>())
        .Callback<EventHandler<Song?>>(h => handler = h);
    using var context = NewNowPlayingContext().Then((_) => handler?.Invoke(null, song));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void OnSongChanged_SongIsNull_UpdatesSongDetails() {
    var song = EntityTestFactory.GenerateSong();
    EventHandler<Song?>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song?>>())
        .Callback<EventHandler<Song?>>(h => handler = h);
    using var context = NewNowPlayingContext()
        .Then((_) => handler?.Invoke(null, song))
        .Then((_) => handler?.Invoke(null, null));
    _screenshotDiffer.AssertEqualsGolden(context);
  }
  [Fact]
  public void OnSongChanged_LoadsAlbumArt() {
    var song = EntityTestFactory.GenerateSong();
    EventHandler<Song?>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song?>>())
        .Callback<EventHandler<Song?>>(h => handler = h);
    using var imageStream = GetImage();
    _mockStreamingClient.Setup(client => client.GetAlbumArtAsync(song.Album, It.IsAny<Func<IEnumerable<AlbumCover>, AlbumCover>>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(imageStream).Verifiable(Times.Once());
    using var context = NewNowPlayingContext().Then((_) => handler?.Invoke(null, song));
    _mockStreamingClient.Verify();
  }

  [Fact]
  public void OnSongChanged_RepeatAlbum_CachesAlbumArt() {
    var song = EntityTestFactory.GenerateSong();
    EventHandler<Song?>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song?>>())
        .Callback<EventHandler<Song?>>(h => handler = h);
    using var imageStream = GetImage();
    _mockStreamingClient.Setup(client => client.GetAlbumArtAsync(song.Album, It.IsAny<Func<IEnumerable<AlbumCover>, AlbumCover>>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(imageStream).Verifiable(Times.Once());
    using var context = NewNowPlayingContext()
        .Then((_) => handler?.Invoke(null, song))
        .Then((_) => handler?.Invoke(null, song));
    _mockStreamingClient.Verify();
  }

  [Fact]
  public void OnSongChanged_NoAlbumArt_ClearsAlbumArt() {
    var song = EntityTestFactory.GenerateSong(noArt: true);
    EventHandler<Song?>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song?>>())
        .Callback<EventHandler<Song?>>(h => handler = h);
    _mockStreamingClient.Setup(client => client.GetAlbumArtAsync(song.Album, It.IsAny<Func<IEnumerable<AlbumCover>, AlbumCover>>(), It.IsAny<CancellationToken>()))
        .Verifiable(Times.Never());
    using var context = NewNowPlayingContext().Then((_) => handler?.Invoke(null, song));
    _mockStreamingClient.Verify();
  }

  [Fact]
  public void OnPositionChanged_UpdatesPosition() {
    EventHandler<TimeSpan>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.PositionChanged += It.IsAny<EventHandler<TimeSpan>>())
        .Callback<EventHandler<TimeSpan>>(h => handler = h);
    _mockPlaybackQueue.SetupGet((ps) => ps.Duration).Returns(TimeSpan.FromMinutes(5));
    using var context = NewNowPlayingContext().Then((_) => handler?.Invoke(null, TimeSpan.Zero));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void OnPositionChanged_UpdatesPosition_MultipleTimes() {
    EventHandler<TimeSpan>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.PositionChanged += It.IsAny<EventHandler<TimeSpan>>())
        .Callback<EventHandler<TimeSpan>>(h => handler = h);
    _mockPlaybackQueue.SetupGet((ps) => ps.Duration).Returns(TimeSpan.FromMinutes(5));
    using var context = NewNowPlayingContext()
        .Then((_) => handler?.Invoke(null, TimeSpan.Zero))
        .Then((_) => handler?.Invoke(null, TimeSpan.FromMinutes(2)));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  /// <summary>
  /// Verifies that pressing the 'v' hotkey successfully toggles the visualizer component.
  /// </summary>
  [Fact]
  public void ToggleVisualization_Hotkey_TogglesVisibility() {
    using var context = NewNowPlayingContext();
    var view = (NowPlayingView)context.App!.TopRunnableView!.SubViews.First();
    
    // Find internal fields via reflection to verify visibility
    var albumArtField = typeof(NowPlayingView).GetField("_albumArtView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var histogramField = typeof(NowPlayingView).GetField("_histogramView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    
    var albumArt = (View?)albumArtField?.GetValue(view);
    var histogram = (View?)histogramField?.GetValue(view);
    
    Assert.NotNull(albumArt);
    Assert.NotNull(histogram);
    
    // Initial state: album art visible, histogram hidden
    Assert.True(albumArt.Visible);
    Assert.False(histogram.Visible);
    
    // Toggle ON with 'v' hotkey
    context.KeyDown(Terminal.Gui.Input.Key.V);
    Assert.False(albumArt.Visible);
    Assert.True(histogram.Visible);
    _mockPlaybackQueue.VerifySet(q => q.IsSpectrumActive = true, Times.Once());
    
    // Toggle OFF
    context.KeyDown(Terminal.Gui.Input.Key.V);
    Assert.True(albumArt.Visible);
    Assert.False(histogram.Visible);
    _mockPlaybackQueue.VerifySet(q => q.IsSpectrumActive = false, Times.Exactly(2));
  }

  /// <summary>
  /// Verifies that running the 'np-vis' command successfully toggles the visualizer component.
  /// </summary>
  [Fact]
  public void ToggleVisualization_Command_TogglesVisibility() {
    using var context = NewNowPlayingContext();
    var view = (NowPlayingView)context.App!.TopRunnableView!.SubViews.First();
    
    var albumArtField = typeof(NowPlayingView).GetField("_albumArtView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var histogramField = typeof(NowPlayingView).GetField("_histogramView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    
    var albumArt = (View?)albumArtField?.GetValue(view);
    var histogram = (View?)histogramField?.GetValue(view);
    
    Assert.NotNull(albumArt);
    Assert.NotNull(histogram);
    
    Assert.True(albumArt.Visible);
    Assert.False(histogram.Visible);
    
    // Toggle ON with command
    context.Then((_) => _commandService.ExecuteCommand("np-vis"));
    Assert.False(albumArt.Visible);
    Assert.True(histogram.Visible);
    _mockPlaybackQueue.VerifySet(q => q.IsSpectrumActive = true, Times.Once());
    
    // Toggle OFF
    context.Then((_) => _commandService.ExecuteCommand("np-vis"));
    Assert.True(albumArt.Visible);
    Assert.False(histogram.Visible);
    _mockPlaybackQueue.VerifySet(q => q.IsSpectrumActive = false, Times.Exactly(2));
  }

  private static Image<Rgba32> GetImage() {
    return new Image<Rgba32>(10, 10);
  }
}