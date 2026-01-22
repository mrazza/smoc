using Moq;
using smoc.Tests.Fakes;
using smoc.Tests.TestInfra;
using Smoc.Services;
using Smoc.Ui;
using Smoc.Ui.Models;
using Terminal.Gui.Views;
using TerminalGuiFluentTesting;

namespace smoc.Tests.Ui;

public class PlayerViewTest {

  private readonly FakeMainWindow _fakeMainWindow;
  private readonly Mock<IPlayerService> _mockPlayerService;
  private readonly CommandService _commandService;
  private readonly ScreenshotDiffer _screenshotDiffer;

  public PlayerViewTest(ITestOutputHelper output) {
    _fakeMainWindow = new FakeMainWindow();
    _mockPlayerService = new Mock<IPlayerService>();
    _commandService = new CommandService();
    _screenshotDiffer = new ScreenshotDiffer(output);
  }

  private TerminalGuiFluentTesting.TestContext NewContext() {
    return With.A<Runnable>(100, 20, TestDriver.ANSI.ToString());
  }

  private PlayerView NewPlayerView() {
    return new PlayerView(_fakeMainWindow, _commandService, _mockPlayerService.Object);
  }

  private TerminalGuiFluentTesting.TestContext NewPlayerViewContext() {
    return NewContext().Add(NewPlayerView());
  }

  [Fact]
  public void InitialState_ShowsEmpty() {
    using var context = NewPlayerViewContext();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void QueueChanged_WithSongs_UpdatesUi() {
    EventHandler? handler = null;
    _mockPlayerService.SetupAdd((ps) => ps.QueueChanged += It.IsAny<EventHandler>())
        .Callback<EventHandler>(h => handler = h);
    _mockPlayerService.Setup((ps) => ps.GetCurrentPlaybackQueue()).Returns([EntityTestFactory.GenerateSong()]);
    using var context = NewContext();
    var playerView = NewPlayerView();
    context.Add(playerView);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void QueueChanged_WithNoSongs_UpdatesUi() {
    EventHandler? handler = null;
    _mockPlayerService.SetupAdd((ps) => ps.QueueChanged += It.IsAny<EventHandler>())
        .Callback<EventHandler>(h => handler = h);
    _mockPlayerService.Setup((ps) => ps.GetCurrentPlaybackQueue()).Returns([EntityTestFactory.GenerateSong()]);
    using var context = NewContext();
    var playerView = NewPlayerView();
    context.Add(playerView).Then((_) => handler?.Invoke(null, EventArgs.Empty));
    _mockPlayerService.Setup((ps) => ps.GetCurrentPlaybackQueue()).Returns([]);
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
}
