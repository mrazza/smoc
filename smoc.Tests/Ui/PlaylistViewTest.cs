using JetBrains.Annotations;
using Moq;
using smoc.Tests.Fakes;
using smoc.Tests.TestInfra;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui;
using Smoc.Ui.Models;
using Terminal.Gui.Drivers;
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
  public void PlaylistCommand_PrevLikedSongs_KeepsPriorState() {
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
    context.Then((_) => _commandService.ExecuteCommand("likes"));

    var song2 = EntityTestFactory.GenerateSong(trackName: "sicker song", postfix: "_2");
    _mockStreamingClient.Setup(client => client.GetLikedSongsAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync([song2]);
    context.Then((_) => _commandService.ExecuteCommand("likes"));
    latch.Release();
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SearchCommand_PlaylistApiFailure_ShowsError() {
    using var context = NewPlaylistViewContext();
    context.Then((_) => _commandService.ExecuteCommand("p/sick playlist"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SearchCommand_PlaylistsMatch_ShowsResults() {
    using var context = NewPlaylistViewContext();
    _mockStreamingClient.Setup(client => client.SearchPlaylistsAsync("sick playlist", It.IsAny<CancellationToken>()))
      .ReturnsAsync([new Playlist("123", "Sickest Playlist"), new Playlist("456", "lame ass shit")]);
    context.Then((_) => _commandService.ExecuteCommand("p/sick playlist"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SearchCommand_NoResults_ShowsMessage() {
    using var context = NewPlaylistViewContext();
    _mockStreamingClient.Setup(client => client.SearchPlaylistsAsync("sick playlist", It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);
    context.Then((_) => _commandService.ExecuteCommand("p/sick playlist"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void PlaylistSelected_ApiError_ShowsError() {
    using var context = NewPlaylistViewContext();
    _mockStreamingClient.Setup(client => client.SearchPlaylistsAsync("sick playlist", It.IsAny<CancellationToken>()))
      .ReturnsAsync([new Playlist("123", "Sickest Playlist"), new Playlist("456", "lame ass shit")]);
    context.Then((_) => _commandService.ExecuteCommand("p/sick playlist")).KeyDown(Key.Enter);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void PlaylistSelected_NoTracks_ShowsMessage() {
    using var context = NewPlaylistViewContext();
    var playlist = new Playlist("123", "Sickest Playlist");
    _mockStreamingClient.Setup(client => client.SearchPlaylistsAsync("sick playlist", It.IsAny<CancellationToken>()))
      .ReturnsAsync([playlist, new Playlist("456", "lame ass shit")]);
    _mockStreamingClient.Setup(client => client.GetPlaylistSongsAsync(playlist, It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);
    context.Then((_) => _commandService.ExecuteCommand("p/sick playlist")).KeyDown(Key.Enter);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void PlaylistSelected_HasTracks_ShowsResults() {
    using var context = NewPlaylistViewContext();
    var playlist = new Playlist("123", "Sickest Playlist");
    _mockStreamingClient.Setup(client => client.SearchPlaylistsAsync("sick playlist", It.IsAny<CancellationToken>()))
      .ReturnsAsync([playlist, new Playlist("456", "lame ass shit")]);
    _mockStreamingClient.Setup(client => client.GetPlaylistSongsAsync(playlist, It.IsAny<CancellationToken>()))
      .ReturnsAsync([EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong()]);
    context.Then((_) => _commandService.ExecuteCommand("p/sick playlist")).KeyDown(Key.Enter);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void PlaylistSongSelected_ShowsContextMenu() {
    using var context = NewPlaylistViewContext();
    var playlist = new Playlist("123", "Sickest Playlist");
    _mockStreamingClient.Setup(client => client.SearchPlaylistsAsync("sick playlist", It.IsAny<CancellationToken>()))
      .ReturnsAsync([playlist, new Playlist("456", "lame ass shit")]);
    _mockStreamingClient.Setup(client => client.GetPlaylistSongsAsync(playlist, It.IsAny<CancellationToken>()))
      .ReturnsAsync([EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong()]);
    context.Then((_) => _commandService.ExecuteCommand("p/sick playlist"))
      .KeyDown(Key.Enter)
      .KeyDown(Key.CursorRight)
      .KeyDown(Key.CursorDown)
      .KeyDown(Key.Enter);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void PlaylistSongSelected_ExecutePlayback_PlaysCorrectSong() {
    using var context = NewPlaylistViewContext();
    var playlist = new Playlist("123", "Sickest Playlist");
    _mockStreamingClient.Setup(client => client.SearchPlaylistsAsync("sick playlist", It.IsAny<CancellationToken>()))
      .ReturnsAsync([playlist, new Playlist("456", "lame ass shit")]);
    var song1 = EntityTestFactory.GenerateSong(postfix: "_1");
    var song2 = EntityTestFactory.GenerateSong(postfix: "_2");
    _mockStreamingClient.Setup(client => client.GetPlaylistSongsAsync(playlist, It.IsAny<CancellationToken>()))
      .ReturnsAsync([song1, song2]);
    context.Then((_) => _commandService.ExecuteCommand("p/sick playlist"))
      .KeyDown(Key.Enter)
      .KeyDown(Key.CursorRight)
      .KeyDown(Key.Enter)
      .KeyDown(Key.Enter);
    _mockPlaybackQueue.Verify((player) => player.ClearPlaybackQueue());
    _mockPlaybackQueue.Verify((player) => player.QueueLast(new List<Song> { song1, song2 }));
    _mockPlaybackQueue.Verify((player) => player.ChangeTrack(0));
  }

  [Fact]
  public void PlaylistCommand_PrevPlaylist_KeepsPriorState() {
    using var context = NewPlaylistViewContext();
    var playlist = new Playlist("123", "Sickest Playlist");
    _mockStreamingClient.Setup(client => client.SearchPlaylistsAsync("sick playlist", It.IsAny<CancellationToken>()))
      .ReturnsAsync([playlist, new Playlist("456", "lame ass shit")]);
    _mockStreamingClient.Setup(client => client.GetPlaylistSongsAsync(playlist, It.IsAny<CancellationToken>()))
      .ReturnsAsync([EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong()]);
    context.Then((_) => _commandService.ExecuteCommand("p/sick playlist")).KeyDown(Key.Enter);
    _screenshotDiffer.AssertEqualsGolden(context);
    context.Then((_) => _commandService.ExecuteCommand("p")).KeyDown(Key.Enter);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SearchCommand_PrevLikedSongs_DisplaysCorrectState() {
    using var context = NewPlaylistViewContext();
    _mockStreamingClient.Setup(x => x.GetLikedSongsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([EntityTestFactory.GenerateSong()]);
    context.Then((_) => _commandService.ExecuteCommand("likes"));

    _mockStreamingClient.Setup(client => client.SearchPlaylistsAsync("sick playlist", It.IsAny<CancellationToken>()))
      .ReturnsAsync([new Playlist("123", "Sickest Playlist"), new Playlist("456", "lame ass shit")]);
    context.Then((_) => _commandService.ExecuteCommand("p/sick playlist"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void LikedSongsCommand_PrevSearchedPlaylist_DisplaysCorrectState() {
    using var context = NewPlaylistViewContext();
    _mockStreamingClient.Setup(client => client.SearchPlaylistsAsync("sick playlist", It.IsAny<CancellationToken>()))
      .ReturnsAsync([new Playlist("123", "Sickest Playlist"), new Playlist("456", "lame ass shit")]);
    context.Then((_) => _commandService.ExecuteCommand("p/sick playlist"));

    _mockStreamingClient.Setup(x => x.GetLikedSongsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([EntityTestFactory.GenerateSong()]);
    context.Then((_) => _commandService.ExecuteCommand("likes"));

    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void LikedSongsCommand_NewSearchCancelsPreviousSearchCommand() {
    using var context = NewPlaylistViewContext();
    using var latch = new AsyncLatch(true);

    _mockStreamingClient.Setup(client => client.SearchPlaylistsAsync("sick playlist", It.IsAny<CancellationToken>()))
      .Returns(latch.GetWaiter().ContinueWith(_ => new List<Playlist>([new Playlist("123", "sick playlist")])));
    context.Then((_) => _commandService.ExecuteCommand("p/sick playlist"));

    var song = EntityTestFactory.GenerateSong(trackName: "sicker song", postfix: "_2");
    _mockStreamingClient.Setup(client => client.GetLikedSongsAsync(It.IsAny<CancellationToken>()))
      .ReturnsAsync([song]);
    context.Then((_) => _commandService.ExecuteCommand("likes"));

    latch.Release();
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SearchCommand_CancelsLikesCommand() {
    using var context = NewPlaylistViewContext();
    using var latch = new AsyncLatch(true);

    var song1 = EntityTestFactory.GenerateSong(trackName: "sick song", postfix: "_1");
    _mockStreamingClient.Setup(client => client.GetLikedSongsAsync(It.IsAny<CancellationToken>()))
      .Returns(latch.GetWaiter().ContinueWith(_ => new List<Song>([song1])));
    context.Then((_) => _commandService.ExecuteCommand("likes"));

    _mockStreamingClient.Setup(client => client.SearchPlaylistsAsync("sick playlist", It.IsAny<CancellationToken>()))
      .ReturnsAsync([new Playlist("123", "Sickest Playlist"), new Playlist("456", "lame ass shit")]);
    context.Then((_) => _commandService.ExecuteCommand("p/sick playlist"));

    latch.Release();
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SearchCommand_CancelsOtherSearchCommand() {
    using var context = NewPlaylistViewContext();
    using var latch = new AsyncLatch(true);

    _mockStreamingClient.Setup(client => client.SearchPlaylistsAsync("sick playlist", It.IsAny<CancellationToken>()))
      .Returns(latch.GetWaiter().ContinueWith(_ => new List<Playlist>([new Playlist("123", "sick playlist")])));
    context.Then((_) => _commandService.ExecuteCommand("p/sick playlist"));

    _mockStreamingClient.Setup(client => client.SearchPlaylistsAsync("sickest playlist", It.IsAny<CancellationToken>()))
      .ReturnsAsync([new Playlist("123", "Sickest Playlist"), new Playlist("456", "lame ass shit")]);
    context.Then((_) => _commandService.ExecuteCommand("p/sickest playlist"));

    latch.Release();
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SearchCommand_CancelsPlaylistSelection() {
    using var context = NewPlaylistViewContext();
    using var latch = new AsyncLatch(true);

    var playlist = new Playlist("123", "sick playlist");
    _mockStreamingClient.Setup(client => client.SearchPlaylistsAsync("sick playlist", It.IsAny<CancellationToken>()))
      .ReturnsAsync([playlist]);

    _mockStreamingClient.Setup(client => client.GetPlaylistSongsAsync(playlist, It.IsAny<CancellationToken>()))
      .Returns(latch.GetWaiter().ContinueWith(_ => new List<Song>([EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong()])));
    context.Then((_) => _commandService.ExecuteCommand("p/sick playlist")).KeyDown(Key.Enter);

    _mockStreamingClient.Setup(client => client.SearchPlaylistsAsync("sickest playlist", It.IsAny<CancellationToken>()))
      .ReturnsAsync([new Playlist("123", "Sickest Playlist"), new Playlist("456", "lame ass shit")]);
    context.Then((_) => _commandService.ExecuteCommand("p/sickest playlist"));

    latch.Release();
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void UrlCommand_ValidUrl_ShowsSongs() {
    using var context = NewPlaylistViewContext();
    _mockStreamingClient.Setup(client => client.GetPlaylistSongsFromUrlAsync("http://best.music.ever/playlist", It.IsAny<CancellationToken>()))
      .ReturnsAsync([EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong(), EntityTestFactory.GenerateSong()]);
    context.Then((_) => _commandService.ExecuteCommand("url/http://best.music.ever/playlist"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void UrlCommand_InvalidUrl_ShowsError() {
    using var context = NewPlaylistViewContext();
    context.Then((_) => _commandService.ExecuteCommand("url/invalid"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void UrlCommand_ValidUrl_NoSongs() {
    using var context = NewPlaylistViewContext();
    _mockStreamingClient.Setup(client => client.GetPlaylistSongsFromUrlAsync("http://best.music.ever/playlist", It.IsAny<CancellationToken>()))
      .ReturnsAsync([]);
    context.Then((_) => _commandService.ExecuteCommand("url/http://best.music.ever/playlist"));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void UrlCommand_SetsMode() {
    _fakeMainWindow.SetMode(Mode.Artist);
    using var context = NewPlaylistViewContext();
    Assert.NotEqual(Mode.Playlist, _fakeMainWindow.CurrentMode);
    context.Then((_) => _commandService.ExecuteCommand("url"));
    Assert.Equal(Mode.Playlist, _fakeMainWindow.CurrentMode);
  }

  [Fact]
  public void UrlCommand_CancelsPreviousSearchCommand() {
    using var context = NewPlaylistViewContext();
    using var latch = new AsyncLatch(true);

    _mockStreamingClient.Setup(client => client.SearchPlaylistsAsync("sick playlist", It.IsAny<CancellationToken>()))
      .Returns(latch.GetWaiter().ContinueWith(_ => new List<Playlist>([new Playlist("123", "sick playlist")])));
    context.Then((_) => _commandService.ExecuteCommand("p/sick playlist"));

    var song = EntityTestFactory.GenerateSong(trackName: "sicker song", postfix: "_2");
    _mockStreamingClient.Setup(client => client.GetPlaylistSongsFromUrlAsync("http://best.music.ever/playlist", It.IsAny<CancellationToken>()))
      .ReturnsAsync([song]);
    context.Then((_) => _commandService.ExecuteCommand("url/http://best.music.ever/playlist"));

    latch.Release();
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

}