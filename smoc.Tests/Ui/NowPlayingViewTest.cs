using Moq;
using smoc.Tests.Fakes;
using smoc.Tests.TestInfra;
using Smoc.Services;
using Smoc.Ui;
using Smoc.Ui.Models;
using Terminal.Gui.Views;
using TerminalGuiFluentTesting;

namespace smoc.Tests.Ui;

public class NowPlayingViewTest {
  private readonly FakeMainWindow _fakeMainWindow;
  private readonly Mock<IPlaybackQueueService> _mockPlaybackQueue;
  private readonly Mock<HttpClientHandler> _mockHttpClientHandler;
  private readonly HttpClient _httpClient;
  private readonly CommandService _commandService;
  private readonly ScreenshotDiffer _screenshotDiffer;

  public NowPlayingViewTest(ITestOutputHelper output) {
    _fakeMainWindow = new FakeMainWindow();
    _mockPlaybackQueue = new Mock<IPlaybackQueueService>();
    _commandService = new CommandService();
    _screenshotDiffer = new ScreenshotDiffer(output);
    _mockHttpClientHandler = new Mock<HttpClientHandler>();
    _httpClient = new HttpClient(_mockHttpClientHandler.Object);
  }

  private static TerminalGuiFluentTesting.TestContext NewContext() => With.A<Runnable>(100, 20, TestDriver.ANSI.ToString());

  private NowPlayingView NewNowPlaying() => new NowPlayingView(_fakeMainWindow, _commandService, _mockPlaybackQueue.Object, _httpClient);

  private TerminalGuiFluentTesting.TestContext NewNowPlayingContext() => NewContext().AddAndLayout(NewNowPlaying());

  [Fact]
  public void NowPlayingCommand_ChangesModeToNowPlaying() {
    _fakeMainWindow.CurrentMode = Mode.Queue;
    using var context = NewNowPlayingContext();

    Assert.NotEqual(Mode.NowPlaying, _fakeMainWindow.CurrentMode);
    context.Then((_) => _commandService.ExecuteCommand("np"));
    Assert.Equal(Mode.NowPlaying, _fakeMainWindow.CurrentMode);
  }
}