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

  [Fact]
  public void ApplyFilter_MatchesSongTitle_CaseInsensitive() {
    using var songTable = NewSongTable();
    var artist = new Artist("1", "Artist");
    var album = new Album("1", artist, "Album", []);
    var song1 = new Song("1", album, "Comfortably Numb", TimeSpan.FromMinutes(6));
    var song2 = new Song("2", album, "Stairway to Heaven", TimeSpan.FromMinutes(8));

    songTable.SetSongs([song1, song2]);
    songTable.ApplyFilter("numb");

    Assert.Single(songTable.GetSongs(), song1);
    Assert.Equal("numb", songTable.FilterText);

    songTable.ApplyFilter("HEAVEN");
    Assert.Single(songTable.GetSongs(), song2);
  }

  [Fact]
  public void ApplyFilter_MatchesArtistName_CaseInsensitive() {
    using var songTable = NewSongTable();
    var artist1 = new Artist("1", "Pink Floyd");
    var artist2 = new Artist("2", "Led Zeppelin");
    var album1 = new Album("1", artist1, "Album 1", []);
    var album2 = new Album("2", artist2, "Album 2", []);
    var song1 = new Song("1", album1, "Track 1", TimeSpan.FromMinutes(6));
    var song2 = new Song("2", album2, "Track 2", TimeSpan.FromMinutes(8));

    songTable.SetSongs([song1, song2]);
    songTable.ApplyFilter("floyd");

    Assert.Single(songTable.GetSongs(), song1);

    songTable.ApplyFilter("LED");
    Assert.Single(songTable.GetSongs(), song2);
  }

  [Fact]
  public void ApplyFilter_MatchesAlbumName_CaseInsensitive() {
    using var songTable = NewSongTable();
    var artist = new Artist("1", "Artist");
    var album1 = new Album("1", artist, "The Dark Side of the Moon", []);
    var album2 = new Album("2", artist, "Abbey Road", []);
    var song1 = new Song("1", album1, "Track 1", TimeSpan.FromMinutes(6));
    var song2 = new Song("2", album2, "Track 2", TimeSpan.FromMinutes(8));

    songTable.SetSongs([song1, song2]);
    songTable.ApplyFilter("dark side");

    Assert.Single(songTable.GetSongs(), song1);

    songTable.ApplyFilter("ABBEY");
    Assert.Single(songTable.GetSongs(), song2);
  }

  [Fact]
  public void ApplyFilter_NoMatch_ReturnsEmpty() {
    using var songTable = NewSongTable();
    var artist = new Artist("1", "Artist");
    var album = new Album("1", artist, "Album", []);
    var song1 = new Song("1", album, "Track 1", TimeSpan.FromMinutes(6));

    songTable.SetSongs([song1]);
    songTable.ApplyFilter("NonexistentQuery");

    Assert.Empty(songTable.GetSongs());
  }

  [Fact]
  public void ClearFilter_RestoresAllSongs() {
    using var songTable = NewSongTable();
    var artist = new Artist("1", "Artist");
    var album = new Album("1", artist, "Album", []);
    var song1 = new Song("1", album, "Track 1", TimeSpan.FromMinutes(6));
    var song2 = new Song("2", album, "Track 2", TimeSpan.FromMinutes(8));

    songTable.SetSongs([song1, song2]);
    songTable.ApplyFilter("Track 1");
    Assert.Single(songTable.GetSongs(), song1);

    songTable.ClearFilter();
    Assert.Equal(2, songTable.GetSongs().Count);
    Assert.Equal(string.Empty, songTable.FilterText);
  }

  [Fact]
  public void EscapeKey_WhenFilterActive_ClearsFilter() {
    using var context = NewContext();
    var songTable = NewSongTable();
    var artist = new Artist("1", "Artist");
    var album = new Album("1", artist, "Album", []);
    var song1 = new Song("1", album, "Track 1", TimeSpan.FromMinutes(6));
    var song2 = new Song("2", album, "Track 2", TimeSpan.FromMinutes(8));

    context.Add(songTable)
        .Then((_) => songTable.SetSongs([song1, song2]))
        .Then((_) => songTable.ApplyFilter("Track 1"));

    Assert.Single(songTable.GetSongs(), song1);
    Assert.Equal("Track 1", songTable.FilterText);

    context.KeyDown(Key.Esc);

    Assert.Equal(2, songTable.GetSongs().Count);
    Assert.Equal(string.Empty, songTable.FilterText);
  }

  [Fact]
  public void SelectingSong_WhenFiltered_ReturnsCorrectSong() {
    using var context = NewContext();
    var songTable = NewSongTable();
    var artist1 = new Artist("1", "Pink Floyd");
    var album1 = new Album("1", artist1, "The Wall", []);
    var song1 = new Song("1", album1, "Comfortably Numb", TimeSpan.FromMinutes(6));
    var artist2 = new Artist("2", "Led Zeppelin");
    var album2 = new Album("2", artist2, "IV", []);
    var song2 = new Song("2", album2, "Stairway to Heaven", TimeSpan.FromMinutes(8));
    var artist3 = new Artist("3", "The Beatles");
    var album3 = new Album("3", artist3, "Abbey Road", []);
    var song3 = new Song("3", album3, "Come Together", TimeSpan.FromMinutes(4));

    List<Song> selectedSongs = [];
    songTable.SongSelected += (_, songs) => selectedSongs.AddRange(songs);

    context.Add(songTable)
        .Then((_) => songTable.SetSongs([song1, song2, song3]))
        .Then((_) => songTable.ApplyFilter("stairway"))
        .KeyDown(Key.Enter);

    Assert.Single(selectedSongs, song2);
  }

  [Fact]
  public void EmptyTable_EnterPressed_DoesNotInvokeSongSelected() {
    using var context = NewContext();
    var songTable = NewSongTable();
    bool songSelectedInvoked = false;
    songTable.SongSelected += (_, _) => songSelectedInvoked = true;

    context.Add(songTable)
        .KeyDown(Key.Enter);

    Assert.False(songSelectedInvoked);
  }

  [Fact]
  public void SongTable_ImplementsIFilterable() {
    using var songTable = NewSongTable();
    Assert.IsAssignableFrom<IFilterable>(songTable);
  }
}
