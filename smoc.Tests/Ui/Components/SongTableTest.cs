using System.Drawing;
using smoc.Tests.TestInfra;
using Smoc.Streaming;
using Smoc.Ui.Components;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using static Smoc.Ui.Components.SongTable;

namespace smoc.Tests.Ui.Components;

public class SongTableTest {

  private readonly ScreenshotDiffer _screenshotDiffer;

  public SongTableTest(ITestOutputHelper output) {
    _screenshotDiffer = new ScreenshotDiffer(output);
  }

  private static AppTestHelper NewContext() => With.A<Runnable>(100, 20, TestDriver.ANSI.ToString());

  private static SongTable NewSongTable(SongTableColumns columns = SongTableColumns.All) => new SongTable(columns) {
    Width = Dim.Fill(),
    Height = Dim.Fill()
  };

  private static AppTestHelper NewSongTableContext(SongTableColumns columns = SongTableColumns.All) =>
    NewContext().Add(NewSongTable(columns));

  [Fact]
  public void EmptyTable_AllColumns_ShowsHeaders() {
    using var context = NewSongTableContext(SongTableColumns.All);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void EmptyTable_SomeColumns_ShowsHeaders() {
    using var context = NewSongTableContext(SongTableColumns.Artist | SongTableColumns.Song);
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SetSongs_DisplaysSongs() {
    using var context = NewContext();
    var songTable = NewSongTable();
    context.Add(songTable)
        .Then((_) => songTable.SetSongs([EntityTestFactory.GenerateSong(postfix: "_1"), EntityTestFactory.GenerateSong(postfix: "_2")]));
    _screenshotDiffer.AssertEqualsGolden(context);
  }

  [Fact]
  public void SetHighlightedRow_StoresRow() {
    using var songTable = NewSongTable();
    songTable.HighlightedRow = 1;
    Assert.Equal(1, songTable.HighlightedRow);
  }

  [Fact]
  public void SetHighlightedRow_HighlightsRow() {
    using var context = NewContext();
    var songTable = NewSongTable();
    context.Add(songTable).Then((_) => {
      songTable.SetSongs([EntityTestFactory.GenerateSong(postfix: "_1"), EntityTestFactory.GenerateSong(postfix: "_2"), EntityTestFactory.GenerateSong(postfix: "_3")]);
      songTable.HighlightedRow = 1;
    });
    _screenshotDiffer.AssertEqualsGolden(context, ansiShot: true);
  }

  [Fact]
  public void SelectingSong_TriggersSelectedEvent() {
    using var context = NewContext();
    var songTable = NewSongTable();
    Song expectedSong = EntityTestFactory.GenerateSong(postfix: "_2");
    List<Song> selectedSongs = [];
    songTable.SongSelected += (_, songs) => selectedSongs.AddRange(songs);
    context.Add(songTable)
        .Then((_) => songTable.SetSongs([EntityTestFactory.GenerateSong(postfix: "_1"), expectedSong, EntityTestFactory.GenerateSong(postfix: "_3")]))
        .KeyDown(Key.CursorDown)
        .KeyDown(Key.CursorDown)
        .KeyDown(Key.Enter);
    Assert.Single(selectedSongs, expectedSong);
  }

  [Fact]
  public void SelectingSongs_TriggersSelectedEvent() {
    using var context = NewContext();
    var songTable = NewSongTable();
    List<Song> expectedSongs = [EntityTestFactory.GenerateSong(postfix: "_1"), EntityTestFactory.GenerateSong(postfix: "_2")];
    List<Song> selectedSongs = [];
    songTable.SongSelected += (_, songs) => selectedSongs.AddRange(songs);
    context.Add(songTable)
        .Then((_) => songTable.SetSongs([.. expectedSongs, EntityTestFactory.GenerateSong(postfix: "_3")]))
        .KeyDown(Key.CursorDown)
        .KeyDown(Key.CursorDown.WithShift)
        .KeyDown(Key.Enter);
    Assert.Equal(expectedSongs, selectedSongs);
  }

  [Fact]
  public void GetSelectedRowFramePosition_ReturnsCorrectPosition() {
    using var context = NewContext();
    var songTable = NewSongTable();
    context.Add(songTable)
        .Then((_) => songTable.SetSongs([EntityTestFactory.GenerateSong(postfix: "_1"), EntityTestFactory.GenerateSong(postfix: "_2"), EntityTestFactory.GenerateSong(postfix: "_3")]))
        .KeyDown(Key.CursorDown)
        .KeyDown(Key.CursorDown);
    Assert.Equal(new Point(0, 2), songTable.GetSelectedRowFramePosition());
  }

  [Fact]
  public void GetSelectedRowScreenPosition_ReturnsCorrectPosition() {
    using var context = NewContext();
    var songTable = NewSongTable();
    context.Add(songTable)
        .Then((_) => songTable.SetSongs([EntityTestFactory.GenerateSong(postfix: "_1"), EntityTestFactory.GenerateSong(postfix: "_2"), EntityTestFactory.GenerateSong(postfix: "_3")]))
        .KeyDown(Key.CursorDown)
        .KeyDown(Key.CursorDown);
    Assert.Equal(new Point(1, 4), songTable.GetSelectedRowScreenPosition());
  }
}
