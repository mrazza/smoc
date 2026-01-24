using Moq;
using smoc.Tests.Fakes;
using smoc.Tests.TestInfra;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui;
using Smoc.Ui.Models;
using Terminal.Gui.Input;
using Terminal.Gui.Views;
using TerminalGuiFluentTesting;

namespace smoc.Tests.Ui;

public class PlayerViewTest {

  private readonly FakeMainWindow _fakeMainWindow;
  private readonly Mock<IPlaybackQueueService> _mockPlaybackQueue;
  private readonly CommandService _commandService;
  private readonly ScreenshotDiffer _screenshotDiffer;

  public PlayerViewTest(ITestOutputHelper output) {
    _fakeMainWindow = new FakeMainWindow();
    _mockPlaybackQueue = new Mock<IPlaybackQueueService>();
    _commandService = new CommandService();
    _screenshotDiffer = new ScreenshotDiffer(output);
  }

  private static TerminalGuiFluentTesting.TestContext NewContext() => With.A<Runnable>(100, 20, TestDriver.ANSI.ToString());

  private PlayerView NewPlayerView() => new PlayerView(_fakeMainWindow, _commandService, _mockPlaybackQueue.Object);

  private TerminalGuiFluentTesting.TestContext NewPlayerViewContext() => NewContext().Add(NewPlayerView());

  [Fact]
  public void InitialState_ShowsEmpty() {
    using var context = NewPlayerViewContext();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void QueueChanged_WithSongs_UpdatesUi() {
    EventHandler? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.QueueChanged += It.IsAny<EventHandler>())
        .Callback<EventHandler>(h => handler = h);
    _mockPlaybackQueue.Setup((ps) => ps.GetCurrentPlaybackQueue()).Returns([EntityTestFactory.GenerateSong()]);
    using var context = NewContext();
    var playerView = NewPlayerView();
    context.Add(playerView).Then((_) => handler?.Invoke(null, EventArgs.Empty));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void QueueChanged_WithNoSongs_UpdatesUi() {
    EventHandler? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.QueueChanged += It.IsAny<EventHandler>())
        .Callback<EventHandler>(h => handler = h);
    _mockPlaybackQueue.Setup((ps) => ps.GetCurrentPlaybackQueue()).Returns([EntityTestFactory.GenerateSong()]);
    using var context = NewContext();
    var playerView = NewPlayerView();
    context.Add(playerView).Then((_) => handler?.Invoke(null, EventArgs.Empty));
    _mockPlaybackQueue.Setup((ps) => ps.GetCurrentPlaybackQueue()).Returns([]);
    context.Then((_) => handler?.Invoke(null, EventArgs.Empty));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void NowPlayingCommand_SetsMode() {
    _fakeMainWindow.CurrentMode = Mode.Artist;
    using var context = NewPlayerViewContext();
    Assert.NotEqual(Mode.Player, _fakeMainWindow.CurrentMode);
    context.Then((_) => _commandService.ExecuteCommand("np"));
    Assert.Equal(Mode.Player, _fakeMainWindow.CurrentMode);
  }

  [Fact]
  public void SongChanged_HighlightsSong() {
    EventHandler<Song>? songChangedHandler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song>>())
        .Callback<EventHandler<Song>>(h => songChangedHandler = h);
    EventHandler? queueChangedHandler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.QueueChanged += It.IsAny<EventHandler>())
        .Callback<EventHandler>(h => queueChangedHandler = h);
    _mockPlaybackQueue.SetupGet((ps) => ps.CurrentPlaybackIndex).Returns(1);
    Song[] songs = [EntityTestFactory.GenerateSong(postfix: "1"), EntityTestFactory.GenerateSong(postfix: "2")];
    _mockPlaybackQueue.Setup((ps) => ps.GetCurrentPlaybackQueue()).Returns(songs);
    using var context = NewContext();
    var playerView = NewPlayerView();
    context.Add(playerView)
        .Then((_) => queueChangedHandler?.Invoke(null, EventArgs.Empty))
        .Then((_) => songChangedHandler?.Invoke(null, songs[1]));
    _screenshotDiffer.AssertEqualsGolden(context, ansiShot: true);
  }

  [Fact]
  public void SongSelected_SetsCurrentSong() {
    EventHandler? queueChangedHandler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.QueueChanged += It.IsAny<EventHandler>())
        .Callback<EventHandler>(h => queueChangedHandler = h);
    _mockPlaybackQueue.Setup((ps) => ps.ChangeTrack(1)).Verifiable(Times.Once());
    _mockPlaybackQueue.Setup((ps) => ps.GetCurrentPlaybackQueue()).Returns([EntityTestFactory.GenerateSong(postfix: "1"), EntityTestFactory.GenerateSong(postfix: "2")]);
    using var context = NewContext();
    var playerView = NewPlayerView();
    context.Add(playerView)
        .Then((_) => queueChangedHandler?.Invoke(null, EventArgs.Empty))
        .KeyDown(Key.CursorDown)
        .KeyDown(Key.Enter);
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void BecomesVisible_SelectsCurrentSong() {
    EventHandler? queueChangedHandler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.QueueChanged += It.IsAny<EventHandler>())
        .Callback<EventHandler>(h => queueChangedHandler = h);
    _mockPlaybackQueue.SetupGet((ps) => ps.CurrentPlaybackIndex).Returns(1);
    Song[] songs = [EntityTestFactory.GenerateSong(postfix: "1"), EntityTestFactory.GenerateSong(postfix: "2")];
    _mockPlaybackQueue.SetupGet((ps) => ps.CurrentSong).Returns(songs[1]);
    _mockPlaybackQueue.Setup((ps) => ps.GetCurrentPlaybackQueue()).Returns(songs);
    using var context = NewContext();
    var playerView = NewPlayerView();
    playerView.Visible = false;
    context.Add(playerView)
        .Then((_) => queueChangedHandler?.Invoke(null, EventArgs.Empty))
        .Then((_) => playerView.Visible = true);
    _screenshotDiffer.AssertEqualsGolden(context, ansiShot: true);
  }
}
