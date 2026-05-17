using Moq;
using smoc.Tests.TestInfra;
using Smoc.Services;
using Smoc.Streaming;
using Terminal.Gui.Views;
using StatusBar = Smoc.Ui.StatusBar;

namespace smoc.Tests.Ui;

public class StatusBarTest {
  private readonly Mock<IPlaybackQueueService> _mockPlaybackQueue;
  private readonly ScreenshotDiffer _screenshotDiffer;

  public StatusBarTest(ITestOutputHelper output) {
    _mockPlaybackQueue = new Mock<IPlaybackQueueService>();
    _screenshotDiffer = new ScreenshotDiffer(output);
  }

  private StatusBar NewStatusBar() => new StatusBar(_mockPlaybackQueue.Object);

  private static AppTestHelper NewContext() => With.A<Runnable>(100, 20, TestDriver.ANSI.ToString()).ConfigureDefaultTheme();

  private AppTestHelper NewStatusBarContext() => NewContext().Add(NewStatusBar());

  [Fact]
  public void InitialState_ShowsEmpty() {
    using var context = NewStatusBarContext();
    _screenshotDiffer.AssertEqualsGolden(context, ansiShot: true);
  }

  [Fact]
  public void SetMode_ShowsMode() {
    using var context = NewContext();
    var statusBar = NewStatusBar();
    context.Add(statusBar).Then((_) => statusBar.SetMode("ARTIST"));
    Assert.Equal("ARTIST", statusBar.GetMode());
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SetMode_ChangesMode() {
    using var context = NewContext();
    var statusBar = NewStatusBar();
    context.Add(statusBar).Then((_) => statusBar.SetMode("ARTIST")).Then((_) => statusBar.SetMode("TRACK"));
    Assert.Equal("TRACK", statusBar.GetMode());
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SetState_ShowsState() {
    using var context = NewContext();
    var statusBar = NewStatusBar();
    context.Add(statusBar).Then((_) => statusBar.SetState("SetState_ShowsState_CoolState"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SongChanged_ShowsState() {
    EventHandler<Song>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song>>()).Callback<EventHandler<Song>>((h) => handler = h);
    Song song = SetupMockPlayerService();
    using var context = NewStatusBarContext().Then((_) => handler?.Invoke(null, song));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SongChanged_UpdatesState() {
    EventHandler<Song>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song>>()).Callback<EventHandler<Song>>((h) => handler = h);
    Song song = SetupMockPlayerService();
    using var context = NewStatusBarContext()
        .Then((_) => handler?.Invoke(null, song))
        .Then((_) => song = SetupMockPlayerService(currentTime: TimeSpan.FromMinutes(2)))
        .Then((_) => handler?.Invoke(null, song));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SongChanged_NoSong_UpdatesState() {
    EventHandler<Song?>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song?>>()).Callback<EventHandler<Song?>>((h) => handler = h);
    Song song = SetupMockPlayerService();
    using var context = NewStatusBarContext()
        .Then((_) => handler?.Invoke(null, song));
    _mockPlaybackQueue.SetupGet((ps) => ps.CurrentSong).Returns((Song?)null);
    context.Then((_) => handler?.Invoke(null, null));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void PositionChanged_ShowsState() {
    EventHandler<TimeSpan>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.PositionChanged += It.IsAny<EventHandler<TimeSpan>>()).Callback<EventHandler<TimeSpan>>((h) => handler = h);
    Song song = SetupMockPlayerService(currentTime: TimeSpan.FromMinutes(1));
    using var context = NewStatusBarContext().Then((_) => handler?.Invoke(null, TimeSpan.FromMinutes(1)));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void PositionChanged_NoTrackOrArtist_ShowsState() {
    EventHandler<TimeSpan>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.PositionChanged += It.IsAny<EventHandler<TimeSpan>>()).Callback<EventHandler<TimeSpan>>((h) => handler = h);
    SetupMockPlayerService();
    _mockPlaybackQueue.SetupGet((ps) => ps.CurrentSong); // Reset this to null.
    using var context = NewStatusBarContext().Then((_) => handler?.Invoke(null, TimeSpan.FromMinutes(1)));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void PlaybackStateChanged_Playing_ShowsState() {
    EventHandler<PlaybackState>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.PlaybackStateChanged += It.IsAny<EventHandler<PlaybackState>>()).Callback<EventHandler<PlaybackState>>((h) => handler = h);
    Song song = SetupMockPlayerService(playbackState: PlaybackState.Playing);
    using var context = NewStatusBarContext().Then((_) => handler?.Invoke(null, PlaybackState.Playing));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void PlaybackStateChanged_Paused_ShowsState() {
    EventHandler<PlaybackState>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.PlaybackStateChanged += It.IsAny<EventHandler<PlaybackState>>()).Callback<EventHandler<PlaybackState>>((h) => handler = h);
    Song song = SetupMockPlayerService(playbackState: PlaybackState.Paused);
    using var context = NewStatusBarContext().Then((_) => handler?.Invoke(null, PlaybackState.Paused));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void PlaybackStateChanged_Stopped_ShowsState() {
    EventHandler<PlaybackState>? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.PlaybackStateChanged += It.IsAny<EventHandler<PlaybackState>>()).Callback<EventHandler<PlaybackState>>((h) => handler = h);
    Song song = SetupMockPlayerService(playbackState: PlaybackState.Stopped);
    using var context = NewStatusBarContext().Then((_) => handler?.Invoke(null, PlaybackState.Stopped));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  private Song SetupMockPlayerService(PlaybackState playbackState = PlaybackState.Playing, Song? currentSong = null, TimeSpan? currentTime = null) {
    _mockPlaybackQueue.SetupGet((ps) => ps.PlaybackState).Returns(playbackState);
    Song song = currentSong ?? EntityTestFactory.GenerateSong();
    _mockPlaybackQueue.SetupGet((ps) => ps.CurrentSong).Returns(song);
    _mockPlaybackQueue.SetupGet((ps) => ps.Duration).Returns(song.Duration);
    _mockPlaybackQueue.SetupGet((ps) => ps.CurrentTime).Returns(currentTime ?? TimeSpan.FromMinutes(1));
    return song;
  }
}