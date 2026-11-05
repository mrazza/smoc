using Moq;
using Smoc.Configuration;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui;
using Smoc.Ui.Components;
using Smoc.Ui.Models;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using smoc.Tests.Fakes;
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

  [Fact]
  public void ArtistSelected_SingleAlbumFailure_ShowsSongsFromRemainingAlbums() {
    using var context = NewArtistViewContext();

    var pinkFloyd = new Artist("123", "Pink Floyd");
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("pink floyd", It.IsAny<CancellationToken>()))
      .ReturnsAsync([pinkFloyd]);

    context.Then((_) => _commandService.ExecuteCommand("a/pink floyd"));

    var darkSide = new Album("321", pinkFloyd, "Animals", [EntityTestFactory.GenerateAlbumCover()], 1977);
    var brokenAlbum = new Album("322", pinkFloyd, "Unavailable Release", [EntityTestFactory.GenerateAlbumCover()], 1975);
    _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(pinkFloyd, It.IsAny<CancellationToken>()))
      .ReturnsAsync([darkSide, brokenAlbum]);

    var time = new Song("456", darkSide, "Dogs", TimeSpan.FromMinutes(17), 2);
    _mockStreamingClient.Setup(client => client.GetSongsByAlbumAsync(darkSide, It.IsAny<CancellationToken>()))
      .ReturnsAsync([time]);
    _mockStreamingClient.Setup(client => client.GetSongsByAlbumAsync(brokenAlbum, It.IsAny<CancellationToken>()))
      .ThrowsAsync(new ArgumentNullException("buttons[2].musicPlayButtonRenderer.playNavigationEndpoint.watchPlaylistEndpoint.playlistId", "Requiered token is null."));

    context.KeyDown(Key.Enter);

    using var textWriter = new StringWriter();
    context.ScreenShot("", textWriter);
    var screen = textWriter.ToString();
    Assert.Contains("Dogs", screen);
    Assert.Contains("Animals", screen);
  }

  [Fact]
  public void ArtistSelected_DeduplicationEnabled_FiltersTeaserSinglesAndPreservesUniqueSingles() {
    var previousConfig = SmocConfiguration.Defaults;
    try {
      SmocConfiguration.Defaults = new SmocConfiguration { DeduplicateArtistSingles = true };
      using var context = NewArtistViewContext();

      var artist = new Artist("123", "Billie Eilish");
      _mockStreamingClient.Setup(client => client.SearchArtistsAsync("billie", It.IsAny<CancellationToken>()))
        .ReturnsAsync([artist]);

      context.Then((_) => _commandService.ExecuteCommand("a/billie"));

      var album = new Album("alb-1", artist, "HIT ME HARD AND SOFT", [EntityTestFactory.GenerateAlbumCover()], 2024, AlbumType.Album);
      var teaserSingle = new Album("sng-1", artist, "CHIHIRO", [EntityTestFactory.GenerateAlbumCover()], 2024, AlbumType.Single);
      var uniqueSingle = new Album("sng-2", artist, "What Was I Made For", [EntityTestFactory.GenerateAlbumCover()], 2025, AlbumType.Single);

      _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(artist, It.IsAny<CancellationToken>()))
        .ReturnsAsync([album, teaserSingle, uniqueSingle]);

      var albumTrack = new Song("trk-1", album, "CHIHIRO", TimeSpan.FromMinutes(5), 1);
      var teaserTrack = new Song("trk-2", teaserSingle, "CHIHIRO", TimeSpan.FromMinutes(5), 1);
      var uniqueTrack = new Song("trk-3", uniqueSingle, "What Was I Made For", TimeSpan.FromMinutes(3), 1);

      _mockStreamingClient.Setup(client => client.GetSongsByAlbumAsync(album, It.IsAny<CancellationToken>()))
        .ReturnsAsync([albumTrack]);
      _mockStreamingClient.Setup(client => client.GetSongsByAlbumAsync(teaserSingle, It.IsAny<CancellationToken>()))
        .ReturnsAsync([teaserTrack]);
      _mockStreamingClient.Setup(client => client.GetSongsByAlbumAsync(uniqueSingle, It.IsAny<CancellationToken>()))
        .ReturnsAsync([uniqueTrack]);

      context.KeyDown(Key.Enter);

      using var textWriter = new StringWriter();
      context.ScreenShot("", textWriter);
      var screen = textWriter.ToString();

      Assert.Contains("HIT ME HARD AND SOFT", screen);
      Assert.Contains("What Was I Made For", screen);
      Assert.DoesNotContain("[Single] CHIHIRO", screen);
    } finally {
      SmocConfiguration.Defaults = previousConfig;
    }
  }

  [Fact]
  public void ArtistSelected_DeduplicationDisabled_PreservesAllSingles() {
    var previousConfig = SmocConfiguration.Defaults;
    try {
      SmocConfiguration.Defaults = new SmocConfiguration { DeduplicateArtistSingles = false };
      using var context = NewArtistViewContext();

      var artist = new Artist("123", "Billie Eilish");
      _mockStreamingClient.Setup(client => client.SearchArtistsAsync("billie", It.IsAny<CancellationToken>()))
        .ReturnsAsync([artist]);

      context.Then((_) => _commandService.ExecuteCommand("a/billie"));

      var album = new Album("alb-1", artist, "HIT ME HARD AND SOFT", [EntityTestFactory.GenerateAlbumCover()], 2024, AlbumType.Album);
      var teaserSingle = new Album("sng-1", artist, "CHIHIRO", [EntityTestFactory.GenerateAlbumCover()], 2024, AlbumType.Single);

      _mockStreamingClient.Setup(client => client.GetAlbumsByArtistAsync(artist, It.IsAny<CancellationToken>()))
        .ReturnsAsync([album, teaserSingle]);

      var albumTrack = new Song("trk-1", album, "CHIHIRO", TimeSpan.FromMinutes(5), 1);
      var teaserTrack = new Song("trk-2", teaserSingle, "CHIHIRO", TimeSpan.FromMinutes(5), 1);

      _mockStreamingClient.Setup(client => client.GetSongsByAlbumAsync(album, It.IsAny<CancellationToken>()))
        .ReturnsAsync([albumTrack]);
      _mockStreamingClient.Setup(client => client.GetSongsByAlbumAsync(teaserSingle, It.IsAny<CancellationToken>()))
        .ReturnsAsync([teaserTrack]);

      context.KeyDown(Key.Enter);

      using var textWriter = new StringWriter();
      context.ScreenShot("", textWriter);
      var screen = textWriter.ToString();

      Assert.Contains("HIT ME HARD AND SOFT", screen);
      Assert.Contains("[Single] CHIHIRO", screen);
    } finally {
      SmocConfiguration.Defaults = previousConfig;
    }
  }

  [Fact]
  public void PlaybackHotKeys_WhenSearchResultsListFocused_TriggerPlaybackQueue() {
    var nowPlayingBar = new NowPlayingBar(_fakeMainWindow, _mockPlaybackQueue.Object, _commandService, _mockStreamingClient.Object);
    var artistView = new ArtistView(_fakeMainWindow, _commandService, _mockStreamingClient.Object, _mockPlaybackQueue.Object);
    var container = new View {
      Width = Dim.Fill(),
      Height = Dim.Fill(),
      CanFocus = true,
    };
    container.Add(nowPlayingBar, artistView);

    using var context = With.A<Runnable>(100, 20, TestDriver.ANSI.ToString())
      .AddAndLayout(container);

    var artists = new List<Artist> {
      new("1", "Radiohead"),
      new("2", "[Soundtrack]"),
      new("3", ",Comma Artist"),
      new("4", ".Period Artist"),
      new("5", "]Right Bracket Artist"),
      new("6", " Space Artist"),
    };
    _mockStreamingClient.Setup(client => client.SearchArtistsAsync("test", It.IsAny<CancellationToken>()))
      .ReturnsAsync(artists);

    context.Then((_) => _commandService.ExecuteCommand("a/test"));

    var searchResults = artistView.SubViews.OfType<SearchResultsList<SearchResultRow<Artist>>>().First();
    Assert.True(searchResults.HasFocus);

    // Verify Space -> PlayPause
    _mockPlaybackQueue.Setup(q => q.PlayPause()).Returns(Task.CompletedTask).Verifiable(Times.Once());
    context.KeyDown(Key.Space);
    _mockPlaybackQueue.Verify(q => q.PlayPause(), Times.Once());

    // Verify Ctrl+Space -> Stop
    _mockPlaybackQueue.Setup(q => q.Stop()).Verifiable(Times.Once());
    context.KeyDown(Key.Space.WithCtrl);
    _mockPlaybackQueue.Verify(q => q.Stop(), Times.Once());

    // Verify , -> PreviousTrack
    _mockPlaybackQueue.Setup(q => q.PreviousTrack(false, null)).Returns(Task.CompletedTask).Verifiable(Times.Once());
    context.KeyDown(NowPlayingBar.PreviousKey);
    _mockPlaybackQueue.Verify(q => q.PreviousTrack(false, null), Times.Once());

    // Verify . -> NextTrack
    _mockPlaybackQueue.Setup(q => q.NextTrack()).Returns(Task.CompletedTask).Verifiable(Times.Once());
    context.KeyDown(NowPlayingBar.NextKey);
    _mockPlaybackQueue.Verify(q => q.NextTrack(), Times.Once());

    // Verify [ -> SeekBackward
    _mockPlaybackQueue.Setup(q => q.SeekBackward(TimeSpan.FromSeconds(10))).Verifiable(Times.Once());
    context.KeyDown(NowPlayingBar.SeekBackwardKey);
    _mockPlaybackQueue.Verify(q => q.SeekBackward(TimeSpan.FromSeconds(10)), Times.Once());

    // Verify ] -> SeekForward
    _mockPlaybackQueue.Setup(q => q.SeekForward(TimeSpan.FromSeconds(10))).Verifiable(Times.Once());
    context.KeyDown(NowPlayingBar.SeekForwardKey);
    _mockPlaybackQueue.Verify(q => q.SeekForward(TimeSpan.FromSeconds(10)), Times.Once());
  }
}
