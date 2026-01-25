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

public class PlaylistViewTest {
  private readonly Mock<IStreamingClient> _mockStreamingClient;
  private readonly Mock<IPlaybackQueueService> _mockPlaybackQueue;
  private readonly CommandService _commandService;
  private readonly FakeMainWindow _fakeMainWindow;
  private readonly ScreenshotDiffer _screenshotDiffer;

  public PlaylistViewTest(ITestOutputHelper output) {
    _mockStreamingClient = new Mock<IStreamingClient>();
    _mockPlaybackQueue = new Mock<IPlaybackQueueService>();
    _fakeMainWindow = new FakeMainWindow();
    _commandService = new CommandService();
    _screenshotDiffer = new ScreenshotDiffer(output);
  }

  private PlaylistView NewPlaylistView() => new(_fakeMainWindow, _commandService, _mockPlaybackQueue.Object, _mockStreamingClient.Object);

  private static TerminalGuiFluentTesting.TestContext NewContext() => With.A<Runnable>(100, 20, TestDriver.ANSI.ToString()).ConfigureDefaultTheme();

  private TerminalGuiFluentTesting.TestContext NewPlaylistViewContext() => NewContext().Add(NewPlaylistView());

  [Fact]
  public void InitialState_ShowsEmpty() {
    using var context = NewPlaylistViewContext();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void PlaylistCommand_SetsMode() {
    _fakeMainWindow.SetMode(Mode.Artist);
    using var context = NewPlaylistViewContext();
    Assert.NotEqual(Mode.Playlist, _fakeMainWindow.CurrentMode);
    context.Then((_) => _commandService.ExecuteCommand("p"));
    Assert.Equal(Mode.Playlist, _fakeMainWindow.CurrentMode);
  }

  [Fact]
  public void LikedSongsCommand_ApiError_ShowsError() {
    using var context = NewPlaylistViewContext();
    context.Then((_) => _commandService.ExecuteCommand("likes"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void LikedSongsCommand_ApiSuccess_ShowsResults() {
    using var context = NewPlaylistViewContext();
    _mockStreamingClient.Setup(x => x.GetLikedSongsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([EntityTestFactory.GenerateSong()]);
    context.Then((_) => _commandService.ExecuteCommand("likes"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void LikedSongsCommand_ApiNoResults_ShowsNoResults() {
    using var context = NewPlaylistViewContext();
    _mockStreamingClient.Setup(x => x.GetLikedSongsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
    context.Then((_) => _commandService.ExecuteCommand("likes"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void OnSongSelected_ShowsContextMenu() {
    using var context = NewPlaylistViewContext();
    _mockStreamingClient.Setup(x => x.GetLikedSongsAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync([EntityTestFactory.GenerateSong(postfix: "_1"), EntityTestFactory.GenerateSong(postfix: "_2"), EntityTestFactory.GenerateSong(postfix: "_3")]);
    context.Then((_) => _commandService.ExecuteCommand("likes")).KeyDown(Key.CursorDown).KeyDown(Key.Enter);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void LikedSongsCommand_Repeat_KeepsPriorState() {
    using var context = NewPlaylistViewContext();
    _mockStreamingClient.Setup(x => x.GetLikedSongsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([EntityTestFactory.GenerateSong()]);
    context.Then((_) => _commandService.ExecuteCommand("likes"));
    _screenshotDiffer.AssertEqualsGolden(context);
    context.Then((_) => _commandService.ExecuteCommand("p"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void LikedSongsCommand_NewSearchCancelsPreviousSearch() {
    using var context = NewPlaylistViewContext();
    using var latch = new AsyncLatch(true);
    var song1 = EntityTestFactory.GenerateSong(trackName: "sick song", postfix: "_1");
    _mockStreamingClient.Setup(client => client.GetLikedSongsAsync(It.IsAny<CancellationToken>()))
      .Returns(latch.GetWaiter().ContinueWith(_ => new List<Song>([song1])));
    var song2 = EntityTestFactory.GenerateSong(trackName: "sicker song", postfix: "_2");

    _mockStreamingClient.Setup(client => client.GetLikedSongsAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync([song2]);

    context.Then((_) => _commandService.ExecuteCommand("likes"));
    context.Then((_) => _commandService.ExecuteCommand("likes"));
    latch.Release();
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }
}