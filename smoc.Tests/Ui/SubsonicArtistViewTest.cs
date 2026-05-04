
using Moq;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui;
using Smoc.Ui.Models;
using Terminal.Gui.Views;
using AppTestHelpers;
using smoc.Tests.Fakes;
using Terminal.Gui.Input;
using smoc.Tests.TestInfra;
using Xunit.Sdk;

namespace smoc.Tests.Ui;

public class SubsonicArtistViewTest {
  private readonly Mock<IStreamingClient> _mockStreamingClient;
  private readonly Mock<IPlaybackQueueService> _mockPlaybackQueue;
  private readonly CommandService _commandService;
  private readonly FakeMainWindow _fakeMainWindow;
  private readonly ScreenshotDiffer _screenshotDiffer;

  public SubsonicArtistViewTest(ITestOutputHelper output) {
    _mockStreamingClient = new Mock<IStreamingClient>();
    _mockPlaybackQueue = new Mock<IPlaybackQueueService>();
    _fakeMainWindow = new FakeMainWindow();
    _commandService = new CommandService();
    _screenshotDiffer = new ScreenshotDiffer(output);
  }

  private AppTestHelper NewArtistViewContext() {
    return With.A<Runnable>(100, 20, TestDriver.ANSI.ToString())
          .AddAndLayout(new ArtistView(_fakeMainWindow, _commandService, _mockStreamingClient.Object, _mockPlaybackQueue.Object));
  }

  [Fact]
  public void SubsonicSearchCommand_ArtistDisplayCorrectly() {
    var artists = new List<Artist> {
      new Artist("s1", "Subsonic Artist 1"),
      new Artist("s2", "Subsonic Artist 2")
    };
    _mockStreamingClient.Setup(c => c.SearchArtistsAsync("subsonic", It.IsAny<CancellationToken>()))
      .ReturnsAsync(artists);

    using var context = NewArtistViewContext();

    context.Then((_) => _commandService.ExecuteCommand("a/subsonic"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }
}
