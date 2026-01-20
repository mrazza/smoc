
using Moq;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui;
using Smoc.Ui.Components;
using Smoc.Ui.Models;
using System.Collections.ObjectModel;
using Terminal.Gui;
using Terminal.Gui.Views; // Required for Window, ListView
using TerminalGuiFluentTesting;
using Xunit;
using System.Text;
using Terminal.Gui.Drivers;
using Acornima.Ast;
using smoc.Tests.Fakes;
using System.ComponentModel.DataAnnotations;
using Terminal.Gui.Input;
using smoc.Tests.Infra;

namespace smoc.Tests;

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
          .Add(new ArtistView(_fakeMainWindow, _commandService, _mockStreamingClient.Object, _mockPlayerService.Object))
          .ResizeConsole(100, 20);
  }

  [Fact]
  public void SearchCommand_ArtistApiFailure_ShowsError() {
    using var context = NewArtistViewContext();

    _commandService.ExecuteCommand("a/radiohead");
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SearchCommand_AlbumLookupApiFailure_ShowsError() {
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
  public void SearchCommand_SongLookupApiFailure_ShowsError() {
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
  public void SearchCommand_SongsFound_ShowsResults() {
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
  public void ArtistCommand_ChangesModeToArtist() {
    _fakeMainWindow.CurrentMode = Mode.Player;
    using var context = NewArtistViewContext();

    _commandService.ExecuteCommand("a");
    context.WaitIteration();
    Assert.Equal(Mode.Artist, _fakeMainWindow.CurrentMode);
  }

  [Fact]
  public void SearchCommand_ChangesModeToArtist() {
    _fakeMainWindow.CurrentMode = Mode.Player;
    using var context = NewArtistViewContext();

    _commandService.ExecuteCommand("a/radiohead");
    context.WaitIteration();
    Assert.Equal(Mode.Artist, _fakeMainWindow.CurrentMode);
  }

  [Fact]
  public void ArtistCommand_ShowsEmptyUi() {
    using var context = NewArtistViewContext();

    _commandService.ExecuteCommand("a");
    context.WaitIteration();
    _screenshotDiffer.AssertEqualsGolden(context);
  }
}
