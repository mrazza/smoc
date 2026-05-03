using Moq;
using smoc.Tests.Fakes;
using smoc.Tests.TestInfra;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui;
using Smoc.Ui.Models;
using Terminal.Gui.Input;
using Terminal.Gui.Views;
using AppTestHelpers;

namespace smoc.Tests.Ui;

public class PlaybackQueueViewTest {

  private readonly FakeMainWindow _fakeMainWindow;
  private readonly Mock<IPlaybackQueueService> _mockPlaybackQueue;
  private readonly CommandService _commandService;
  private readonly ScreenshotDiffer _screenshotDiffer;

  public PlaybackQueueViewTest(ITestOutputHelper output) {
    _fakeMainWindow = new FakeMainWindow();
    _mockPlaybackQueue = new Mock<IPlaybackQueueService>();
    _commandService = new CommandService();
    _screenshotDiffer = new ScreenshotDiffer(output);
  }

  private static AppTestHelper NewContext() => With.A<Runnable>(100, 20, TestDriver.ANSI.ToString());

  private PlaybackQueueView PlaybackQueueView() => new PlaybackQueueView(_fakeMainWindow, _commandService, _mockPlaybackQueue.Object);

  private AppTestHelper PlaybackQueueViewContext() => NewContext().Add(PlaybackQueueView());

  [Fact]
  public void InitialState_ShowsEmpty() {
    using var context = PlaybackQueueViewContext();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void QueueChanged_WithSongs_UpdatesUi() {
    EventHandler? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.QueueChanged += It.IsAny<EventHandler>())
        .Callback<EventHandler>(h => handler = h);
    _mockPlaybackQueue.Setup((ps) => ps.GetCurrentPlaybackQueue()).Returns([EntityTestFactory.GenerateSong()]);
    using var context = NewContext();
    var queueView = PlaybackQueueView();
    context.Add(queueView).Then((_) => handler?.Invoke(null, EventArgs.Empty));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void QueueChanged_WithNoSongs_UpdatesUi() {
    EventHandler? handler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.QueueChanged += It.IsAny<EventHandler>())
        .Callback<EventHandler>(h => handler = h);
    _mockPlaybackQueue.Setup((ps) => ps.GetCurrentPlaybackQueue()).Returns([EntityTestFactory.GenerateSong()]);
    using var context = NewContext();
    var queueView = PlaybackQueueView();
    context.Add(queueView).Then((_) => handler?.Invoke(null, EventArgs.Empty));
    _mockPlaybackQueue.Setup((ps) => ps.GetCurrentPlaybackQueue()).Returns([]);
    context.Then((_) => handler?.Invoke(null, EventArgs.Empty));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void QueueCommand_SetsMode() {
    _fakeMainWindow.CurrentMode = Mode.Artist;
    using var context = PlaybackQueueViewContext();
    Assert.NotEqual(Mode.Queue, _fakeMainWindow.CurrentMode);
    context.Then((_) => _commandService.ExecuteCommand("pq"));
    Assert.Equal(Mode.Queue, _fakeMainWindow.CurrentMode);
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
    var queueView = PlaybackQueueView();
    context.Add(queueView)
        .Then((_) => queueChangedHandler?.Invoke(null, EventArgs.Empty))
        .Then((_) => songChangedHandler?.Invoke(null, songs[1]));
    _screenshotDiffer.AssertEqualsGolden(context, ansiShot: true);
  }

  [Fact]
  public void SongChanged_NoSong_Ok() {
    EventHandler<Song?>? songChangedHandler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.SongChanged += It.IsAny<EventHandler<Song?>>())
        .Callback<EventHandler<Song?>>(h => songChangedHandler = h);
    _mockPlaybackQueue.SetupGet((ps) => ps.CurrentPlaybackIndex).Returns(0);
    _mockPlaybackQueue.Setup((ps) => ps.GetCurrentPlaybackQueue()).Returns([]);
    using var context = NewContext();
    var queueView = PlaybackQueueView();
    context.Add(queueView)
        .Then((_) => songChangedHandler?.Invoke(null, null));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SongSelected_SetsCurrentSong() {
    EventHandler? queueChangedHandler = null;
    _mockPlaybackQueue.SetupAdd((ps) => ps.QueueChanged += It.IsAny<EventHandler>())
        .Callback<EventHandler>(h => queueChangedHandler = h);
    _mockPlaybackQueue.Setup((ps) => ps.ChangeTrack(1)).Verifiable(Times.Once());
    _mockPlaybackQueue.Setup((ps) => ps.GetCurrentPlaybackQueue()).Returns([EntityTestFactory.GenerateSong(postfix: "1"), EntityTestFactory.GenerateSong(postfix: "2")]);
    using var context = NewContext();
    var queueView = PlaybackQueueView();
    context.Add(queueView)
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
    var queueView = PlaybackQueueView();
    queueView.Visible = false;
    context.Add(queueView)
        .Then((_) => queueChangedHandler?.Invoke(null, EventArgs.Empty))
        .Then((_) => queueView.Visible = true);
    _screenshotDiffer.AssertEqualsGolden(context, ansiShot: true);
  }
}
