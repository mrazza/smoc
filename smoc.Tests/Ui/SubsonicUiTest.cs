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

public class SubsonicUiTest {
  private readonly Mock<IStreamingClient> _mockStreamingClient;
  private readonly Mock<IPlaybackQueueService> _mockPlaybackQueue;
  private readonly CommandService _commandService;
  private readonly FakeMainWindow _fakeMainWindow;
  private readonly ScreenshotDiffer _screenshotDiffer;

  public SubsonicUiTest(ITestOutputHelper output) {
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
  public void SubsonicArtistSearch_ShowsResults() {
    using var context = NewArtistViewContext();

    var artist = new Artist("sub-artist-1", "Subsonic Artist");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("Subsonic", It.IsAny<CancellationToken>()))
      .ReturnsAsync([artist]);

    context.Then((_) => _commandService.ExecuteCommand("a/Subsonic"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SubsonicAlbumSelection_ShowsSongs() {
    using var context = NewArtistViewContext();

    var artist = new Artist("sub-artist-1", "Subsonic Artist");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("Subsonic", It.IsAny<CancellationToken>()))
      .ReturnsAsync([artist]);

    context.Then((_) => _commandService.ExecuteCommand("a/Subsonic"));

    var album = new Album("sub-album-1", artist, "Subsonic Album", [EntityTestFactory.GenerateAlbumCover()], 2024);
    _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(artist, It.IsAny<CancellationToken>()))
      .ReturnsAsync([album]);
    
    var song = new Song("sub-song-1", album, "Subsonic Track", TimeSpan.FromMinutes(4), 1);
    _mockStreamingClient.Setup(client => client.GetSongsByAlbumAsync(album, It.IsAny<CancellationToken>()))
      .ReturnsAsync([song]);

    context.KeyDown(Key.Enter);
    _screenshotDiffer.AssertEqualsGolden(context);
  }
}
