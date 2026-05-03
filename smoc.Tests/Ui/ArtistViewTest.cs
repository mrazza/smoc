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

public class ArtistViewTest {
  private readonly Mock<IStreamingClient> _mockStreamingClient;
  private readonly Mock<IPlaybackQueueService> _mockPlaybackQueue;
  private readonly CommandService _commandService;
  private readonly FakeMainWindow _fakeMainWindow;
  private readonly ScreenshotDiffer _screenshotDiffer;

  public ArtistViewTest(ITestOutputHelper output) {
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
  public void SearchCommand_ChangesModeToArtist() {
    _fakeMainWindow.CurrentMode = Mode.Queue;
    using var context = NewArtistViewContext();

    Assert.NotEqual(Mode.Artist, _fakeMainWindow.CurrentMode);
    context.Then((_) => _commandService.ExecuteCommand("a/radiohead"));
    Assert.Equal(Mode.Artist, _fakeMainWindow.CurrentMode);
  }

  [Fact]
  public void SearchCommand_ArtistApiFailure_ShowsError() {
    using var context = NewArtistViewContext();

    context.Then((_) => _commandService.ExecuteCommand("a/radiohead"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SearchCommand_ArtistsMatches_ShowsResults() {
    using var context = NewArtistViewContext();

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([radiohead]);

    context.Then((_) => _commandService.ExecuteCommand("a/radiohead"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SearchCommand_NoArtists_ShowsNoResult() {
    using var context = NewArtistViewContext();

    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);

    context.Then((_) => _commandService.ExecuteCommand("a/radiohead"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }


  [Fact]
  public void ArtistSelected_AlbumLookupApiFailure_ShowsError() {
    using var context = NewArtistViewContext();

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([radiohead]);

    context
      .Then((_) => _commandService.ExecuteCommand("a/radiohead"))
      .KeyDown(Key.Enter);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void ArtistSelected_SongLookupApiFailure_ShowsError() {
    using var context = NewArtistViewContext();

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([radiohead]);

    context.Then((_) => _commandService.ExecuteCommand("a/radiohead"));

    var okComputer = new Album("321", radiohead, "OK Computer", [EntityTestFactory.GenerateAlbumCover()], 1970);
    _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(radiohead, It.IsAny<CancellationToken>()))
      .ReturnsAsync([okComputer]);

    context.KeyDown(Key.Enter);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void ArtistSelected_SongsFound_ShowsResults() {
    using var context = NewArtistViewContext();

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([radiohead]);

    context.Then((_) => _commandService.ExecuteCommand("a/radiohead"));

    var okComputer = new Album("321", radiohead, "OK Computer", [EntityTestFactory.GenerateAlbumCover()], 1970);
    _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(radiohead, It.IsAny<CancellationToken>()))
      .ReturnsAsync([okComputer]);
    var paranoidAndroid = new Song("456", okComputer, "Paranoid Android", TimeSpan.FromMinutes(5), 1);
    var climbingUpTheWalls = new Song("456", okComputer, "Climbing Up the Walls", TimeSpan.FromMinutes(3), 2);
    _mockStreamingClient.Setup(client => client.GetSongsByAlbumAsync(okComputer, It.IsAny<CancellationToken>()))
      .ReturnsAsync([paranoidAndroid, climbingUpTheWalls]);
    context.KeyDown(Key.Enter);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SongSelected_ShowsContextWindow() {
    using var context = NewArtistViewContext();

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([radiohead]);

    context.Then((_) => _commandService.ExecuteCommand("a/radiohead"));

    var okComputer = new Album("321", radiohead, "OK Computer", [EntityTestFactory.GenerateAlbumCover()], 1970);
    _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(radiohead, It.IsAny<CancellationToken>()))
      .ReturnsAsync([okComputer]);
    var paranoidAndroid = new Song("456", okComputer, "Paranoid Android", TimeSpan.FromMinutes(5), 1);
    var climbingUpTheWalls = new Song("456", okComputer, "Climbing Up the Walls", TimeSpan.FromMinutes(3), 2);
    _mockStreamingClient.Setup(client => client.GetSongsByAlbumAsync(okComputer, It.IsAny<CancellationToken>()))
      .ReturnsAsync([paranoidAndroid, climbingUpTheWalls]);
    context.KeyDown(Key.Enter).KeyDown(Key.CursorRight).KeyDown(Key.Enter);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SongSelected_ExecutePlayback_PlaysCorrectSong() {
    using var context = NewArtistViewContext();

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([radiohead]);

    context.Then((_) => _commandService.ExecuteCommand("a/radiohead"));

    var okComputer = new Album("321", radiohead, "OK Computer", [EntityTestFactory.GenerateAlbumCover()], 1970);
    _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(radiohead, It.IsAny<CancellationToken>()))
      .ReturnsAsync([okComputer]);
    var paranoidAndroid = new Song("456", okComputer, "Paranoid Android", TimeSpan.FromMinutes(5), 1);
    var climbingUpTheWalls = new Song("456", okComputer, "Climbing Up the Walls", TimeSpan.FromMinutes(3), 2);
    _mockStreamingClient.Setup(client => client.GetSongsByAlbumAsync(okComputer, It.IsAny<CancellationToken>()))
      .ReturnsAsync([paranoidAndroid, climbingUpTheWalls]);
    context.KeyDown(Key.Enter).KeyDown(Key.CursorRight).KeyDown(Key.Enter).KeyDown(Key.Enter);
    _mockPlaybackQueue.Verify((player) => player.ClearPlaybackQueue());
    _mockPlaybackQueue.Verify((player) => player.QueueLast(new List<Song> { paranoidAndroid, climbingUpTheWalls }));
    _mockPlaybackQueue.Verify((player) => player.ChangeTrack(0));
  }

  [Fact]
  public void ArtistSelected_NoAlbums_ShowsNoSongs() {
    using var context = NewArtistViewContext();

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([radiohead]);

    context.Then((_) => _commandService.ExecuteCommand("a/radiohead"));

    _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(radiohead, It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);
    context.KeyDown(Key.Enter);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void ArtistSelected_NoSongs_ShowsNoSongs() {
    using var context = NewArtistViewContext();

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([radiohead]);

    context.Then((_) => _commandService.ExecuteCommand("a/radiohead"));

    var okComputer = new Album("321", radiohead, "OK Computer", [EntityTestFactory.GenerateAlbumCover()], 1970);
    _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(radiohead, It.IsAny<CancellationToken>()))
      .ReturnsAsync([okComputer]);
    _mockStreamingClient.Setup(client => client.GetSongsByAlbumAsync(okComputer, It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);
    context.KeyDown(Key.Enter);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void ArtistCommand_ChangesModeToArtist() {
    _fakeMainWindow.CurrentMode = Mode.Queue;
    using var context = NewArtistViewContext();

    Assert.NotEqual(Mode.Artist, _fakeMainWindow.CurrentMode);
    context.Then((_) => _commandService.ExecuteCommand("a"));
    Assert.Equal(Mode.Artist, _fakeMainWindow.CurrentMode);
  }

  [Fact]
  public void ArtistCommand_FirstTime_ShowsEmptyUi() {
    using var context = NewArtistViewContext();

    context.Then((_) => _commandService.ExecuteCommand("a"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void ArtistCommand_Repeat_KeepsPriorState() {
    using var context = NewArtistViewContext();

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([radiohead]);

    context.Then((_) => _commandService.ExecuteCommand("a/radiohead"));

    var okComputer = new Album("321", radiohead, "OK Computer", [EntityTestFactory.GenerateAlbumCover()], 1970);
    _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(radiohead, It.IsAny<CancellationToken>()))
      .ReturnsAsync([okComputer]);
    var paranoidAndroid = new Song("456", okComputer, "Paranoid Android", TimeSpan.FromMinutes(5), 1);
    var climbingUpTheWalls = new Song("456", okComputer, "Climbing Up the Walls", TimeSpan.FromMinutes(3), 2);
    _mockStreamingClient.Setup(client => client.GetSongsByAlbumAsync(okComputer, It.IsAny<CancellationToken>()))
      .ReturnsAsync([paranoidAndroid, climbingUpTheWalls]);
    context.KeyDown(Key.Enter);

    using var priorTextWriter = new StringWriter();
    context.ScreenShot("", priorTextWriter);

    _fakeMainWindow.SetMode(Mode.Queue);
    context.Then((_) => _commandService.ExecuteCommand("a"));

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

    context.Then((_) => _commandService.ExecuteCommand("a/radiohead"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void ArtistSelected_ShowsLoading() {
    using var context = NewArtistViewContext();

    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .ReturnsAsync([radiohead]);

    context.Then((_) => _commandService.ExecuteCommand("a/radiohead"));

    using var latch = new AsyncLatch(true);
    _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(radiohead, It.IsAny<CancellationToken>()))
      .Returns(latch.GetWaiter().ContinueWith(_ => new List<Album>([])));
    context.KeyDown(Key.Enter);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SearchCommand_NewSearchCancelsPreviousSearch() {
    using var context = NewArtistViewContext();
    using var latch = new AsyncLatch(true);
    var radiohead = new Artist("123", "Radiohead");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("radiohead", It.IsAny<CancellationToken>()))
      .Returns(latch.GetWaiter().ContinueWith(_ => new List<Artist>([radiohead])));

    context.Then((_) => _commandService.ExecuteCommand("a/radiohead"));

    var goose = new Artist("345", "Goose");

    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("goose", It.IsAny<CancellationToken>()))
      .ReturnsAsync([goose]);

    context.Then((_) => _commandService.ExecuteCommand("a/goose"));
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

    context.Then((_) => _commandService.ExecuteCommand("a/radiohead"));

    using var latch = new AsyncLatch(true);
    var okComputer = new Album("321", radiohead, "OK Computer", [EntityTestFactory.GenerateAlbumCover()], 1970);
    var nokayComputer = new Album("322", almostRadiohead, "NOKAY Computer", [EntityTestFactory.GenerateAlbumCover()], 1970);
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

    context
      .KeyDown(Key.Enter)
      .KeyDown(Key.CursorDown)
      .KeyDown(Key.Enter);

    // We've now selected the first artist and, while it was loading, selected the second artist.
    // The first artist should be cancelled and results for the second artist should be shown.
    // Let the first artist complete and verify that the content is unchanged.
    latch.Release();
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }
}
