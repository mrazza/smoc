using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using Acornima.Ast;
using Castle.Components.DictionaryAdapter.Xml;
using Moq;
using smoc.Tests.TestInfra;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui.Components;
using Terminal.Gui.Input;
using Terminal.Gui.Views;
using TerminalGuiFluentTesting;
using static Smoc.Ui.Components.SongTable;

namespace smoc.Tests.Ui.Components;

public class SongContextMenuTest : IDisposable {
  private readonly Mock<IPlaybackQueueService> _mockPlaybackQueue;
  private readonly SongTable _songTable;
  private readonly ScreenshotDiffer _screenshotDiffer;

  public SongContextMenuTest(ITestOutputHelper output) {
    _mockPlaybackQueue = new Mock<IPlaybackQueueService>(MockBehavior.Strict);
    _songTable = new SongTable(SongTableColumns.All);
    _screenshotDiffer = new ScreenshotDiffer(output);
  }

  private static TerminalGuiFluentTesting.TestContext NewContext() => With.A<Runnable>(100, 20, TestDriver.ANSI.ToString());

  private SongContextMenu NewSongContextMenu() => new(_mockPlaybackQueue.Object, _songTable);

  [Fact]
  public void MakeVisible_ShouldDisplayCorrectly() {
    using var context = NewContext();
    var songContextMenu = NewSongContextMenu();
    context.Add(songContextMenu);
    context.Then((_) => songContextMenu.MakeVisible());
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void MakeVisible_ShouldSetFocusToFirstItem() {
    using var context = NewContext();
    var songContextMenu = NewSongContextMenu();
    context.Add(songContextMenu);
    context.Then((_) => songContextMenu.MakeVisible());
    Assert.Equal(SongContextMenu.Messages.PLAY_ALL, songContextMenu.MostFocused?.Title);
  }

  [Fact]
  public void DownArrow_ShouldMoveFocusToNextItem() {
    using var context = NewContext();
    var songContextMenu = NewSongContextMenu();
    context.Add(songContextMenu);
    context
      .Then((_) => songContextMenu.MakeVisible())
      .KeyDown(Key.CursorDown);
    Assert.Equal(SongContextMenu.Messages.PLAY_SELECTION, songContextMenu.MostFocused?.Title);
  }

  [Fact]
  public void VimDown_ShouldMoveFocusToNextItem() {
    using var context = NewContext();
    var songContextMenu = NewSongContextMenu();
    context.Add(songContextMenu);
    context
      .Then((_) => songContextMenu.MakeVisible())
      .KeyDown(Key.J);
    Assert.Equal(SongContextMenu.Messages.PLAY_SELECTION, songContextMenu.MostFocused?.Title);
  }

  [Fact]
  public void UpArrow_ShouldMoveFocusToPreviousItem() {
    using var context = NewContext();
    var songContextMenu = NewSongContextMenu();
    context.Add(songContextMenu);
    context
      .Then((_) => songContextMenu.MakeVisible())
      .KeyDown(Key.CursorDown)
      .KeyDown(Key.CursorUp);
    Assert.Equal(SongContextMenu.Messages.PLAY_ALL, songContextMenu.MostFocused?.Title);
  }

  [Fact]
  public void VimUp_ShouldMoveFocusToPreviousItem() {
    using var context = NewContext();
    var songContextMenu = NewSongContextMenu();
    context.Add(songContextMenu);
    context
      .Then((_) => songContextMenu.MakeVisible())
      .KeyDown(Key.K)
      .KeyDown(Key.J);
    Assert.Equal(SongContextMenu.Messages.PLAY_ALL, songContextMenu.MostFocused?.Title);
  }

  [Fact]
  public void PlayAll_QueuesAllTracks() {
    using var context = NewContext();
    var songContextMenu = NewSongContextMenu();
    context.Add(songContextMenu);
    var radiohead = new Artist("123", "Radiohead");
    var okComputer = new Album("321", radiohead, "OK Computer", 1970, "http://url.com/thumb.jpg");
    var paranoidAndroid = new Song("456", okComputer, "Paranoid Android", TimeSpan.FromMinutes(5), 1);
    var climbingUpTheWalls = new Song("457", okComputer, "Climbing Up the Walls", TimeSpan.FromMinutes(3), 2);
    List<Song> songs = [paranoidAndroid, climbingUpTheWalls];
    _songTable.SetSongs(songs);
    _songTable.SelectedRow = 1;
    _mockPlaybackQueue.Setup((p) => p.ClearPlaybackQueue()).Verifiable(Times.Once());
    _mockPlaybackQueue.Setup((p) => p.QueueLast(songs)).Verifiable(Times.Once());
    _mockPlaybackQueue.Setup((p) => p.ChangeTrack(1)).Returns(Task.CompletedTask).Verifiable(Times.Once());
    _mockPlaybackQueue.Setup((p) => p.Play()).Returns(Task.CompletedTask).Verifiable(Times.Once());
    context
      .Then((_) => songContextMenu.MakeVisible())
      .KeyDown(Key.Enter);
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void PlaySelection__MultiTracks_QueuesSelectedTracks() {
    using var context = NewContext();
    var songContextMenu = NewSongContextMenu();
    context.Add(songContextMenu);
    var radiohead = new Artist("123", "Radiohead");
    var okComputer = new Album("321", radiohead, "OK Computer", 1970, "http://url.com/thumb.jpg");
    var paranoidAndroid = new Song("456", okComputer, "Paranoid Android", TimeSpan.FromMinutes(5), 1);
    var climbingUpTheWalls = new Song("457", okComputer, "Climbing Up the Walls", TimeSpan.FromMinutes(3), 2);
    var climbingDownTheWalls = new Song("458", okComputer, "Climbing Down the Walls", TimeSpan.FromMinutes(3), 2);

    _songTable.SetSongs([paranoidAndroid, climbingUpTheWalls, climbingDownTheWalls]);
    _songTable.SelectedRow = 1;
    _songTable.MultiSelectedRegions.Push(new TableSelection(new Point(0, 1), new Rectangle(0, 1, 0, 2)));
    _mockPlaybackQueue.Setup((p) => p.ClearPlaybackQueue()).Verifiable(Times.Once());
    _mockPlaybackQueue.Setup((p) => p.QueueLast(new List<Song> { climbingUpTheWalls, climbingDownTheWalls })).Verifiable(Times.Once());
    _mockPlaybackQueue.Setup((p) => p.ChangeTrack(0)).Returns(Task.CompletedTask).Verifiable(Times.Once());
    _mockPlaybackQueue.Setup((p) => p.Play()).Returns(Task.CompletedTask).Verifiable(Times.Once());
    context
      .Then((_) => songContextMenu.MakeVisible())
      .KeyDown(Key.CursorDown)
      .KeyDown(Key.Enter);
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void PlaySelection_SingleTrack_QueuesSelectedTrack() {
    using var context = NewContext();
    var songContextMenu = NewSongContextMenu();
    context.Add(songContextMenu);
    var radiohead = new Artist("123", "Radiohead");
    var okComputer = new Album("321", radiohead, "OK Computer", 1970, "http://url.com/thumb.jpg");
    var paranoidAndroid = new Song("456", okComputer, "Paranoid Android", TimeSpan.FromMinutes(5), 1);
    var climbingUpTheWalls = new Song("457", okComputer, "Climbing Up the Walls", TimeSpan.FromMinutes(3), 2);
    var climbingDownTheWalls = new Song("458", okComputer, "Climbing Down the Walls", TimeSpan.FromMinutes(3), 2);

    _songTable.SetSongs([paranoidAndroid, climbingUpTheWalls, climbingDownTheWalls]);
    _songTable.SelectedRow = 1;
    _mockPlaybackQueue.Setup((p) => p.ClearPlaybackQueue()).Verifiable(Times.Once());
    _mockPlaybackQueue.Setup((p) => p.QueueLast(new List<Song> { climbingUpTheWalls })).Verifiable(Times.Once());
    _mockPlaybackQueue.Setup((p) => p.ChangeTrack(0)).Returns(Task.CompletedTask).Verifiable(Times.Once());
    _mockPlaybackQueue.Setup((p) => p.Play()).Returns(Task.CompletedTask).Verifiable(Times.Once());
    context
      .Then((_) => songContextMenu.MakeVisible())
      .KeyDown(Key.CursorDown)
      .KeyDown(Key.Enter);
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void QueueNext_MultiTracks_QueuesSelectedTracks() {
    using var context = NewContext();
    var songContextMenu = NewSongContextMenu();
    context.Add(songContextMenu);
    var radiohead = new Artist("123", "Radiohead");
    var okComputer = new Album("321", radiohead, "OK Computer", 1970, "http://url.com/thumb.jpg");
    var paranoidAndroid = new Song("456", okComputer, "Paranoid Android", TimeSpan.FromMinutes(5), 1);
    var climbingUpTheWalls = new Song("457", okComputer, "Climbing Up the Walls", TimeSpan.FromMinutes(3), 2);
    var climbingDownTheWalls = new Song("458", okComputer, "Climbing Down the Walls", TimeSpan.FromMinutes(3), 2);

    _songTable.SetSongs([paranoidAndroid, climbingUpTheWalls, climbingDownTheWalls]);
    _songTable.SelectedRow = 1;
    _songTable.MultiSelectedRegions.Push(new TableSelection(new Point(0, 1), new Rectangle(0, 1, 0, 2)));
    _mockPlaybackQueue.Setup((p) => p.QueueNext(new List<Song> { climbingUpTheWalls, climbingDownTheWalls })).Verifiable(Times.Once());
    context
      .Then((_) => songContextMenu.MakeVisible())
      .KeyDown(Key.CursorDown)
      .KeyDown(Key.CursorDown)
      .KeyDown(Key.Enter);
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void QueueNext_SingleTrack_QueuesSelectedTrack() {
    using var context = NewContext();
    var songContextMenu = NewSongContextMenu();
    context.Add(songContextMenu);
    var radiohead = new Artist("123", "Radiohead");
    var okComputer = new Album("321", radiohead, "OK Computer", 1970, "http://url.com/thumb.jpg");
    var paranoidAndroid = new Song("456", okComputer, "Paranoid Android", TimeSpan.FromMinutes(5), 1);
    var climbingUpTheWalls = new Song("457", okComputer, "Climbing Up the Walls", TimeSpan.FromMinutes(3), 2);
    var climbingDownTheWalls = new Song("458", okComputer, "Climbing Down the Walls", TimeSpan.FromMinutes(3), 2);

    _songTable.SetSongs([paranoidAndroid, climbingUpTheWalls, climbingDownTheWalls]);
    _songTable.SelectedRow = 1;
    _mockPlaybackQueue.Setup((p) => p.QueueNext(new List<Song> { climbingUpTheWalls })).Verifiable(Times.Once());
    context
      .Then((_) => songContextMenu.MakeVisible())
      .KeyDown(Key.CursorDown)
      .KeyDown(Key.CursorDown)
      .KeyDown(Key.Enter);
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void QueueLast_MultiTracks_QueuesSelectedTracks() {
    using var context = NewContext();
    var songContextMenu = NewSongContextMenu();
    context.Add(songContextMenu);
    var radiohead = new Artist("123", "Radiohead");
    var okComputer = new Album("321", radiohead, "OK Computer", 1970, "http://url.com/thumb.jpg");
    var paranoidAndroid = new Song("456", okComputer, "Paranoid Android", TimeSpan.FromMinutes(5), 1);
    var climbingUpTheWalls = new Song("457", okComputer, "Climbing Up the Walls", TimeSpan.FromMinutes(3), 2);
    var climbingDownTheWalls = new Song("458", okComputer, "Climbing Down the Walls", TimeSpan.FromMinutes(3), 2);

    _songTable.SetSongs([paranoidAndroid, climbingUpTheWalls, climbingDownTheWalls]);
    _songTable.SelectedRow = 1;
    _songTable.MultiSelectedRegions.Push(new TableSelection(new Point(0, 1), new Rectangle(0, 1, 0, 2)));
    _mockPlaybackQueue.Setup((p) => p.QueueLast(new List<Song> { climbingUpTheWalls, climbingDownTheWalls })).Verifiable(Times.Once());
    context
      .Then((_) => songContextMenu.MakeVisible())
      .KeyDown(Key.CursorDown)
      .KeyDown(Key.CursorDown)
      .KeyDown(Key.CursorDown)
      .KeyDown(Key.Enter);
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void QueueLast_SingleTrack_QueuesSelectedTrack() {
    using var context = NewContext();
    var songContextMenu = NewSongContextMenu();
    context.Add(songContextMenu);
    var radiohead = new Artist("123", "Radiohead");
    var okComputer = new Album("321", radiohead, "OK Computer", 1970, "http://url.com/thumb.jpg");
    var paranoidAndroid = new Song("456", okComputer, "Paranoid Android", TimeSpan.FromMinutes(5), 1);
    var climbingUpTheWalls = new Song("457", okComputer, "Climbing Up the Walls", TimeSpan.FromMinutes(3), 2);
    var climbingDownTheWalls = new Song("458", okComputer, "Climbing Down the Walls", TimeSpan.FromMinutes(3), 2);

    _songTable.SetSongs([paranoidAndroid, climbingUpTheWalls, climbingDownTheWalls]);
    _songTable.SelectedRow = 1;
    _mockPlaybackQueue.Setup((p) => p.QueueLast(new List<Song> { climbingUpTheWalls })).Verifiable(Times.Once());
    context
      .Then((_) => songContextMenu.MakeVisible())
      .KeyDown(Key.CursorDown)
      .KeyDown(Key.CursorDown)
      .KeyDown(Key.CursorDown)
      .KeyDown(Key.Enter);
    _mockPlaybackQueue.Verify();
  }

  [Fact]
  public void DownArrow_BottomLoopsToTop() {
    using var context = NewContext();
    var songContextMenu = NewSongContextMenu();
    context.Add(songContextMenu);
    context
      .Then((_) => songContextMenu.MakeVisible())
      .KeyDown(Key.CursorDown)
      .KeyDown(Key.CursorDown)
      .KeyDown(Key.CursorDown)
      .KeyDown(Key.CursorDown);
    Assert.Equal(SongContextMenu.Messages.PLAY_ALL, songContextMenu.MostFocused?.Title);
  }

  [Fact]
  public void UpArrow_TopLoopsToBottom() {
    using var context = NewContext();
    var songContextMenu = NewSongContextMenu();
    context.Add(songContextMenu);
    context
      .Then((_) => songContextMenu.MakeVisible())
      .KeyDown(Key.CursorUp);
    Assert.Equal(SongContextMenu.Messages.ADD_TO_QUEUE, songContextMenu.MostFocused?.Title);
  }

  [Fact]
  public void RequiredHeight_ReturnsFour() {
    var songContextMenu = NewSongContextMenu();
    Assert.Equal(4, songContextMenu.RequiredHeight);
  }

  [Fact]
  public void Dispose_RemovesPopoverFromApp() {
    using var context = NewContext();
    var songContextMenu = NewSongContextMenu();
    context.Add(songContextMenu);
    Assert.Equal(songContextMenu, Assert.Single(context.App?.Popover?.Popovers!));
    context.Then((_) => songContextMenu.Dispose());
    Assert.Empty(context.App?.Popover?.Popovers!);
  }

  public void Dispose() {
    _songTable.Dispose();
  }
}
