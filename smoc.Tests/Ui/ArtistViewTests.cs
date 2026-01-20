using Moq;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui;
using Smoc.Ui.Models;
using Terminal.Gui.Views;
using TerminalGuiFluentTesting;
using smoc.Tests.Fakes;
using Terminal.Gui.Input;
using smoc.Tests.TestInfra;
using SoundFlow.Providers;
using Microsoft.VisualBasic;

namespace smoc.Tests.Ui;

public class ArtistViewTests {
  private readonly Mock<IStreamingClient> _mockStreamingClient;
  private readonly Mock<IPlayerService> _mockPlayerService;
  private readonly CommandService _commandService;
  private readonly FakeMainWindow _fakeMainWindow;
  private readonly ScreenshotDiffer _screenshotDiffer;

  public ArtistViewTests(ITestOutputHelper output) {
    _mockStreamingClient = new Mock<IStreamingClient>();
    _mockPlayerService = new Mock<IPlayerService>();
    _fakeMainWindow = new FakeMainWindow();
    _commandService = new CommandService();
    _screenshotDiffer = new ScreenshotDiffer(output);
  }

  private TerminalGuiFluentTesting.TestContext NewArtistViewContext() {
    return With.A<Runnable>(100, 20, TestDriver.DotNet.ToString())
          .Add(new ArtistView(_fakeMainWindow, _commandService, _mockStreamingClient.Object, _mockPlayerService.Object));
  }

  [Fact]
  public void SearchCommand_ChangesModeToArtist() {
    _fakeMainWindow.CurrentMode = Mode.Player;
    using var context = NewArtistViewContext();

    Assert.NotEqual(Mode.Artist, _fakeMainWindow.CurrentMode);
    _commandService.ExecuteCommand("a/radiohead");
    context.WaitIteration();
    Assert.Equal(Mode.Artist, _fakeMainWindow.CurrentMode);
  }

  [Fact]
  public void SearchCommand_ArtistApiFailure_ShowsError() {
    using var context = NewArtistViewContext();

    _commandService.ExecuteCommand("a/radiohead");
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SearchCommand_ArtistsMatches_ShowsResults() {
    using var context = NewArtistViewContext();

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([radiohead]);

    _commandService.ExecuteCommand("a/radiohead");
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void ArtistSelected_AlbumLookupApiFailure_ShowsError() {
    using var context = NewArtistViewContext();

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([radiohead]);

    _commandService.ExecuteCommand("a/radiohead");
    context.WaitIteration();

    context.KeyDown(Key.Enter);
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void ArtistSelected_SongLookupApiFailure_ShowsError() {
    using var context = NewArtistViewContext();

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([radiohead]);

    _commandService.ExecuteCommand("a/radiohead");
    context.WaitIteration();

    var okComputer = new Album("321", radiohead, "OK Computer", 1970, "http://url.com/thumb.jpg");
    _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(radiohead, It.IsAny<CancellationToken>()))
      .ReturnsAsync([okComputer]);

    context.KeyDown(Key.Enter);
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void ArtistSelected_SongsFound_ShowsResults() {
    using var context = NewArtistViewContext();

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([radiohead]);

    _commandService.ExecuteCommand("a/radiohead");
    context.WaitIteration();

    var okComputer = new Album("321", radiohead, "OK Computer", 1970, "http://url.com/thumb.jpg");
    _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(radiohead, It.IsAny<CancellationToken>()))
      .ReturnsAsync([okComputer]);
    var paranoidAndroid = new Song("456", okComputer, "Paranoid Android", TimeSpan.FromMinutes(5), 1);
    var climbingUpTheWalls = new Song("456", okComputer, "Climbing Up the Walls", TimeSpan.FromMinutes(3), 2);
    _mockStreamingClient.Setup(client => client.GetSongsByAlbumAsync(okComputer, It.IsAny<CancellationToken>()))
      .ReturnsAsync([paranoidAndroid, climbingUpTheWalls]);
    context.KeyDown(Key.Enter);
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void ArtistSelected_NoAlbums_ShowsNoSongs() {
    using var context = NewArtistViewContext();

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([radiohead]);

    _commandService.ExecuteCommand("a/radiohead");
    context.WaitIteration();

    _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(radiohead, It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);
    context.KeyDown(Key.Enter);
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void ArtistSelected_NoSongs_ShowsNoSongs() {
    using var context = NewArtistViewContext();

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([radiohead]);

    _commandService.ExecuteCommand("a/radiohead");
    context.WaitIteration();

    var okComputer = new Album("321", radiohead, "OK Computer", 1970, "http://url.com/thumb.jpg");
    _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(radiohead, It.IsAny<CancellationToken>()))
      .ReturnsAsync([okComputer]);
    _mockStreamingClient.Setup(client => client.GetSongsByAlbumAsync(okComputer, It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);
    context.KeyDown(Key.Enter);
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void ArtistCommand_ChangesModeToArtist() {
    _fakeMainWindow.CurrentMode = Mode.Player;
    using var context = NewArtistViewContext();

    Assert.NotEqual(Mode.Artist, _fakeMainWindow.CurrentMode);
    _commandService.ExecuteCommand("a");
    context.WaitIteration();
    Assert.Equal(Mode.Artist, _fakeMainWindow.CurrentMode);
  }

  [Fact]
  public void ArtistCommand_FirstTime_ShowsEmptyUi() {
    using var context = NewArtistViewContext();

    _commandService.ExecuteCommand("a");
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void ArtistCommand_Repeat_KeepsPriorState() {
    using var context = NewArtistViewContext();

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([radiohead]);

    _commandService.ExecuteCommand("a/radiohead");
    context.WaitIteration();

    var okComputer = new Album("321", radiohead, "OK Computer", 1970, "http://url.com/thumb.jpg");
    _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(radiohead, It.IsAny<CancellationToken>()))
      .ReturnsAsync([okComputer]);
    var paranoidAndroid = new Song("456", okComputer, "Paranoid Android", TimeSpan.FromMinutes(5), 1);
    var climbingUpTheWalls = new Song("456", okComputer, "Climbing Up the Walls", TimeSpan.FromMinutes(3), 2);
    _mockStreamingClient.Setup(client => client.GetSongsByAlbumAsync(okComputer, It.IsAny<CancellationToken>()))
      .ReturnsAsync([paranoidAndroid, climbingUpTheWalls]);
    context.KeyDown(Key.Enter);
    context.WaitIteration();

    using var priorTextWriter = new StringWriter();
    context.ScreenShot("", priorTextWriter);

    _fakeMainWindow.SetMode(Mode.Player);
    _commandService.ExecuteCommand("a");
    context.WaitIteration();

    Assert.Equal(Mode.Artist, _fakeMainWindow.CurrentMode);

    using var afterTextWriter = new StringWriter();
    context.ScreenShot("", afterTextWriter);

    Assert.Equal(priorTextWriter.ToString(), afterTextWriter.ToString());
  }

  [Fact]
  public void SearchCommand_ShowsSearching() {
    using var context = NewArtistViewContext();
    using var latch = new AsyncLatch(true);

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .Returns(latch.GetWaiter().ContinueWith(_ => new List<Artist>([radiohead])));

    _commandService.ExecuteCommand("a/radiohead");
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void ArtistSelected_ShowsLoading() {
    using var context = NewArtistViewContext();

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([radiohead]);

    _commandService.ExecuteCommand("a/radiohead");
    context.WaitIteration();

    using var latch = new AsyncLatch(true);
    _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(radiohead, It.IsAny<CancellationToken>()))
      .Returns(latch.GetWaiter().ContinueWith(_ => new List<Album>([])));
    context.KeyDown(Key.Enter);
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SearchCommand_NewSearchCancelsPreviousSearch() {
    using var context = NewArtistViewContext();
    using var latch = new AsyncLatch(true);

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .Returns(latch.GetWaiter().ContinueWith(_ => new List<Artist>([radiohead])));

    _commandService.ExecuteCommand("a/radiohead");
    context.WaitIteration();

    var goose = new Artist("345", "Goose");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("goose", It.IsAny<CancellationToken>()))
      .ReturnsAsync([goose]);

    _commandService.ExecuteCommand("a/goose");
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);

    // Release the latch allowing the previous search to complete
    // and verify that the content is unchanged.
    latch.Release();
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void ArtistSelected_NewSelectionCancelsPreviousSelection() {
    using var context = NewArtistViewContext();

    var radiohead = new Artist("123", "Radiohead");
    var almostRadiohead = new Artist("124", "Almost Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([radiohead, almostRadiohead]);

    _commandService.ExecuteCommand("a/radiohead");
    context.WaitIteration();

    using var latch = new AsyncLatch(true);
    var okComputer = new Album("321", radiohead, "OK Computer", 1970, "http://url.com/thumb.jpg");
    var nokayComputer = new Album("322", almostRadiohead, "NOKAY Computer", 1970, "http://url.com/thumb.jpg");
    _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(radiohead, It.IsAny<CancellationToken>()))
      .ReturnsAsync([okComputer]);
    _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(almostRadiohead, It.IsAny<CancellationToken>()))
      .ReturnsAsync([nokayComputer]);

    var paranoidAndroid = new Song("456", okComputer, "Paranoid Android", TimeSpan.FromMinutes(5), 1);
    var climbingDownTheWalls = new Song("457", nokayComputer, "Climbing Down the Walls", TimeSpan.FromMinutes(3), 2);
    _mockStreamingClient.Setup(client => client.GetSongsByAlbumAsync(okComputer, It.IsAny<CancellationToken>()))
      .Returns(latch.GetWaiter().ContinueWith(_ => new List<Song>([paranoidAndroid])));
    _mockStreamingClient.Setup(client => client.GetSongsByAlbumAsync(nokayComputer, It.IsAny<CancellationToken>()))
      .ReturnsAsync([climbingDownTheWalls]);
    context.KeyDown(Key.Enter);
    context.WaitIteration();

    context.KeyDown(Key.CursorDown);
    context.WaitIteration();
    context.KeyDown(Key.Enter);
    context.WaitIteration();

    // We've now selected the first artist and, while it was loading, selected the second artist.
    // The first artist should be cancelled and results for the second artist should be shown.
    // Let the first artist complete and verify that the content is unchanged.
    _screenshotDiffer.AssertEqualsGolden(context);
    latch.Release();
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }
}
