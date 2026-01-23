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

public class SongViewTest {
  private readonly Mock<IStreamingClient> _mockStreamingClient;
  private readonly Mock<IPlayerService> _mockPlayerService;
  private readonly CommandService _commandService;
  private readonly FakeMainWindow _fakeMainWindow;
  private readonly ScreenshotDiffer _screenshotDiffer;

  public SongViewTest(ITestOutputHelper output) {
    _mockStreamingClient = new Mock<IStreamingClient>();
    _mockPlayerService = new Mock<IPlayerService>();
    _fakeMainWindow = new FakeMainWindow();
    _commandService = new CommandService();
    _screenshotDiffer = new ScreenshotDiffer(output);
  }

  private SongView NewSongView() => new(_fakeMainWindow, _commandService, _mockStreamingClient.Object, _mockPlayerService.Object);

  private static TerminalGuiFluentTesting.TestContext NewContext() => With.A<Runnable>(100, 20, TestDriver.ANSI.ToString());

  private TerminalGuiFluentTesting.TestContext NewSongViewContext() => NewContext().Add(NewSongView());

  [Fact]
  public void InitialState_ShowsEmpty() {
    using var context = NewSongViewContext();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void TrackCommand_SetsMode() {
    _fakeMainWindow.SetMode(Mode.Artist);
    using var context = NewSongViewContext();
    Assert.NotEqual(Mode.Song, _fakeMainWindow.CurrentMode);
    context.Then((_) => _commandService.ExecuteCommand("t"));
    Assert.Equal(Mode.Song, _fakeMainWindow.CurrentMode);
  }

  [Fact]
  public void TrackCommand_NoQuery_DoesNothing() {
    using var context = NewSongViewContext();
    context.Then((_) => _commandService.ExecuteCommand("t"));
    _screenshotDiffer.AssertEqualsGolden(context);
    _mockStreamingClient.VerifyNoOtherCalls();
  }

  [Fact]
  public void TrackSearchCommand_ApiError_ShowsError() {
    using var context = NewSongViewContext();
    context.Then((_) => _commandService.ExecuteCommand("t/sick song"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void TrackSearchCommand_ApiSuccess_ShowsResults() {
    using var context = NewSongViewContext();
    _mockStreamingClient.Setup(x => x.SearchSongsAsync("sick song", It.IsAny<CancellationToken>())).ReturnsAsync([EntityTestFactory.GenerateSong()]);
    context.Then((_) => _commandService.ExecuteCommand("t/sick song"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void TrackSearchCommand_ApiNoResults_ShowsNoResults() {
    using var context = NewSongViewContext();
    _mockStreamingClient.Setup(x => x.SearchSongsAsync("sick song", It.IsAny<CancellationToken>())).ReturnsAsync([]);
    context.Then((_) => _commandService.ExecuteCommand("t/sick song"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void OnSongSelected_ShowsContextMenu() {
    using var context = NewSongViewContext();
    _mockStreamingClient.Setup(x => x.SearchSongsAsync("sick song", It.IsAny<CancellationToken>()))
        .ReturnsAsync([EntityTestFactory.GenerateSong(postfix: "_1"), EntityTestFactory.GenerateSong(postfix: "_2"), EntityTestFactory.GenerateSong(postfix: "_3")]);
    context.Then((_) => _commandService.ExecuteCommand("t/sick song")).KeyDown(Key.CursorDown).KeyDown(Key.Enter);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void TrackCommand_Repeat_KeepsPriorState() {
    using var context = NewSongViewContext();
    _mockStreamingClient.Setup(x => x.SearchSongsAsync("sick song", It.IsAny<CancellationToken>())).ReturnsAsync([EntityTestFactory.GenerateSong()]);
    context.Then((_) => _commandService.ExecuteCommand("t/sick song"));
    _screenshotDiffer.AssertEqualsGolden(context);
    context.Then((_) => _commandService.ExecuteCommand("t"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void TrackSearchCommand_NewSearchCancelsPreviousSearch() {
    using var context = NewSongViewContext();
    using var latch = new AsyncLatch(true);
    var song1 = EntityTestFactory.GenerateSong(trackName: "sick song", postfix: "_1");
    _mockStreamingClient.Setup(client => client.SearchSongsAsync("sick song", It.IsAny<CancellationToken>()))
      .Returns(latch.GetWaiter().ContinueWith(_ => new List<Song>([song1])));
    var song2 = EntityTestFactory.GenerateSong(trackName: "sicker song", postfix: "_2");

    _mockStreamingClient.Setup(client => client.SearchSongsAsync("sicker song", It.IsAny<CancellationToken>()))
      .ReturnsAsync([song2]);

    context.Then((_) => _commandService.ExecuteCommand("t/sick song"));
    context.Then((_) => _commandService.ExecuteCommand("t/sicker song"));
    latch.Release();
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }
}