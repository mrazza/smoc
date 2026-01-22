using Moq;
using smoc.Tests.TestInfra;
using Smoc.Services;
using Smoc.Streaming;
using Terminal.Gui.Configuration;
using Terminal.Gui.Views;
using TerminalGuiFluentTesting;
using StatusBar = Smoc.Ui.StatusBar;

namespace smoc.Tests.Ui;

public class StatusBarTest {
  private readonly Mock<IPlayerService> _mockPlayerService;
  private readonly ScreenshotDiffer _screenshotDiffer;

  public StatusBarTest(ITestOutputHelper output) {
    _mockPlayerService = new Mock<IPlayerService>();
    _screenshotDiffer = new ScreenshotDiffer(output);
  }

  private StatusBar NewStatusBar() {
    return new StatusBar(_mockPlayerService.Object);
  }

  private TerminalGuiFluentTesting.TestContext NewContext() {
    ConfigurationManager.RuntimeConfig = """
      {
        "Themes": [
            {
                "default": {
                    "Schemes": [
                        {
                            "StatusBar": {
                                "Normal": {
                                    "Foreground": "#949494",
                                    "Background": "#3a3a3a"
                                }
                            }
                        },
                        {
                            "StatusBar_Mode": {
                                "Normal": {
                                    "Foreground": "#262626",
                                    "Background": "#949494",
                                    "Style": "Bold"
                                }
                            }
                        }
                    ]
                }
            }
        ]
    }
    """;
    ConfigurationManager.Enable(ConfigLocations.Runtime);
    return With.A<Runnable>(100, 20, TestDriver.ANSI.ToString());
  }

  private TerminalGuiFluentTesting.TestContext NewStatusBarContext() {
    return NewContext().Add(NewStatusBar());
  }

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
    _mockPlayerService.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song>>()).Callback<EventHandler<Song>>((h) => handler = h);
    Song song = SetupMockPlayerService();
    using var context = NewStatusBarContext().Then((_) => handler?.Invoke(null, song));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SongChanged_UpdatesState() {
    EventHandler<Song>? handler = null;
    _mockPlayerService.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song>>()).Callback<EventHandler<Song>>((h) => handler = h);
    Song song = SetupMockPlayerService();
    using var context = NewStatusBarContext()
        .Then((_) => handler?.Invoke(null, song))
        .Then((_) => song = SetupMockPlayerService(currentTime: TimeSpan.FromMinutes(2)))
        .Then((_) => handler?.Invoke(null, song));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void PositionChanged_ShowsState() {
    EventHandler<TimeSpan>? handler = null;
    _mockPlayerService.SetupAdd((ps) => ps.PositionChanged += It.IsAny<EventHandler<TimeSpan>>()).Callback<EventHandler<TimeSpan>>((h) => handler = h);
    Song song = SetupMockPlayerService(currentTime: TimeSpan.FromMinutes(1));
    using var context = NewStatusBarContext().Then((_) => handler?.Invoke(null, TimeSpan.FromMinutes(1)));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void PositionChanged_NoTrackOrArtist_ShowsState() {
    EventHandler<TimeSpan>? handler = null;
    _mockPlayerService.SetupAdd((ps) => ps.PositionChanged += It.IsAny<EventHandler<TimeSpan>>()).Callback<EventHandler<TimeSpan>>((h) => handler = h);
    SetupMockPlayerService();
    _mockPlayerService.Setup((ps) => ps.CurrentSong).Returns<Song?>(null);
    using var context = NewStatusBarContext().Then((_) => handler?.Invoke(null, TimeSpan.FromMinutes(1)));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void PlaybackStateChanged_Playing_ShowsState() {
    EventHandler<PlaybackState>? handler = null;
    _mockPlayerService.SetupAdd((ps) => ps.PlaybackStateChanged += It.IsAny<EventHandler<PlaybackState>>()).Callback<EventHandler<PlaybackState>>((h) => handler = h);
    Song song = SetupMockPlayerService(playbackState: PlaybackState.Playing);
    using var context = NewStatusBarContext().Then((_) => handler?.Invoke(null, PlaybackState.Playing));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void PlaybackStateChanged_Paused_ShowsState() {
    EventHandler<PlaybackState>? handler = null;
    _mockPlayerService.SetupAdd((ps) => ps.PlaybackStateChanged += It.IsAny<EventHandler<PlaybackState>>()).Callback<EventHandler<PlaybackState>>((h) => handler = h);
    Song song = SetupMockPlayerService(playbackState: PlaybackState.Paused);
    using var context = NewStatusBarContext().Then((_) => handler?.Invoke(null, PlaybackState.Paused));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void PlaybackStateChanged_Stopped_ShowsState() {
    EventHandler<PlaybackState>? handler = null;
    _mockPlayerService.SetupAdd((ps) => ps.PlaybackStateChanged += It.IsAny<EventHandler<PlaybackState>>()).Callback<EventHandler<PlaybackState>>((h) => handler = h);
    Song song = SetupMockPlayerService(playbackState: PlaybackState.Stopped);
    using var context = NewStatusBarContext().Then((_) => handler?.Invoke(null, PlaybackState.Stopped));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  private Song SetupMockPlayerService(PlaybackState playbackState = PlaybackState.Playing, Song? currentSong = null, TimeSpan? currentTime = null) {
    _mockPlayerService.Setup((ps) => ps.PlaybackState).Returns(playbackState);
    Song song = currentSong ?? EntityTestFactory.GenerateSong();
    _mockPlayerService.Setup((ps) => ps.CurrentSong).Returns(song);
    _mockPlayerService.Setup((ps) => ps.Duration).Returns(song.Duration);
    _mockPlayerService.Setup((ps) => ps.CurrentTime).Returns(currentTime ?? TimeSpan.FromMinutes(1));

    return song;
  }
}