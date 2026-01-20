using System.CommandLine;
using System.Data;
using System.Drawing;
using Smoc.Streaming;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Smoc.Ui.Components;

/// <summary>
/// A table view for displaying a list of songs with configurable columns.
/// </summary>
public sealed class SongTable : TableView {
  /// <summary>
  /// Flags indicating which columns to display in the table.
  /// </summary>
  [Flags]
  public enum SongTableColumns {
    Number = 0x1,
    Artist = 0x2,
    Album = 0x4,
    Song = 0x8,
    Length = 0x10,
    Year = 0x20,

    All = Number | Artist | Album | Song | Length | Year
  }

  private readonly DataTable _songTableData;
  private readonly SongTableColumns _columns;
  private List<Song> _songs;
  private int _highlightedRow = -1;

  /// <summary>
  /// Gets or sets the index of the highlighted row.
  /// </summary>
  /// <remarks>
  /// The highlighted row is distinct from the selected row. The highlighted row
  /// is bolded and not the row triggered for activation. It can be used to identify
  /// a row that is important but not currently selected by the user -- say the currently
  /// playing song.
  /// </remarks>
  public int HighlightedRow {
    get => _highlightedRow;
    set {
      if (_highlightedRow != value) {

        _highlightedRow = value;
        SetNeedsDraw();
      }
    }
  }

  /// <summary>
  /// Occurs when a song (or set of songs) is selected (e.g. by pressing Enter).
  /// </summary>
  public event EventHandler<List<Song>>? SongSelected;

  /// <summary>
  /// Initializes a new instance of the <see cref="SongTable"/> class.
  /// </summary>
  /// <param name="columns">Flags to specify which columns to show.</param>
  public SongTable(SongTableColumns columns = SongTableColumns.Number | SongTableColumns.Album | SongTableColumns.Song | SongTableColumns.Length | SongTableColumns.Year)
      : base() {
    FullRowSelect = true;

    Style.SmoothHorizontalScrolling = true;
    Style.AlwaysShowHeaders = true;
    Style.ShowVerticalCellLines = false;
    Style.ShowVerticalHeaderLines = false;
    Style.ShowHorizontalHeaderOverline = false;
    Style.ShowHorizontalHeaderUnderline = false;
    BorderStyle = LineStyle.Single;

    try {
      var highlightedScheme = SchemeManager.GetScheme("TableCurrentTrack");
      var normalScheme = SchemeManager.GetScheme("TableNormalTracks");
      Style.RowColorGetter = (args) => args.RowIndex == _highlightedRow ? highlightedScheme : normalScheme;
    } catch (KeyNotFoundException) {
      Logging.Error("SchemeManager.GetScheme() failed to find required schemes");
    }

    _columns = columns;
    _songTableData = CreateDataTable(columns);
    Table = new DataTableSource(_songTableData);
    _songs = [];
    CanFocus = false;

    // We don't bind left and right as directional keys here because the table
    // selects the entire row and we want to use left and right for navigation.
    VimKeyBindings.AddDirectionalKeyBindings(KeyBindings, bindLeftRight: false);
    VimKeyBindings.AddNavigationKeyBindings(KeyBindings, bindUpDown: false);
    KeyBindings.Remove(Key.CursorRight);
    KeyBindings.Remove(Key.CursorLeft);
    KeyBindings.Remove(Key.Space);
  }

  /// <summary>
  /// Sets the list of songs to display in the table.
  /// </summary>
  /// <param name="songs">The songs to display.</param>
  public void SetSongs(IEnumerable<Song> songs) {
    ClearSongs();
    CanFocus = true;
    _songs = songs.ToList();
    foreach (var song in _songs) {
      int index = 0;
      var values = new object[_songTableData.Columns.Count];
      if (_columns.HasFlag(SongTableColumns.Number)) {
        values[index++] = song.TrackNumber ?? 0;
      }
      if (_columns.HasFlag(SongTableColumns.Artist)) {
        values[index++] = song.Artist.Name;
      }
      if (_columns.HasFlag(SongTableColumns.Album)) {
        values[index++] = song.Album.Name;
      }
      if (_columns.HasFlag(SongTableColumns.Song)) {
        values[index++] = song.Title;
      }
      if (_columns.HasFlag(SongTableColumns.Length)) {
        values[index++] = song.Duration.ToString("m\\:ss");
      }
      if (_columns.HasFlag(SongTableColumns.Year)) {
        values[index++] = song.Album.ReleaseYear ?? 0;
      }
      _songTableData.Rows.Add(values);
    }
  }

  protected override void OnHasFocusChanged(bool newHasFocus, View? previousFocusedView, View? focusedView) {
    base.OnHasFocusChanged(newHasFocus, previousFocusedView, focusedView);

    if (newHasFocus && SelectedRow == -1 && _songTableData.Rows.Count > 0) {
      SelectedRow = 0;
    }
  }

  protected override bool OnCellActivated(CellActivatedEventArgs args) {
    SongSelected?.Invoke(this, GetSelectedSongs());
    return base.OnCellActivated(args);
  }

  /// <summary>
  /// Gets the list of songs currently in the table.
  /// </summary>
  public List<Song> GetSongs() {
    return _songs.ToList();
  }

  /// <summary>
  /// Gets the list of currently selected songs.
  /// </summary>
  public List<Song> GetSelectedSongs() {
    return MultiSelectedRegions.Where(_ => MultiSelect).Select(region => Enumerable.Range(region.Rectangle.Y, region.Rectangle.Height))
        .FirstOrDefault()?.Select(index => _songs[index]).ToList() ?? [_songs[SelectedRow]];
  }

  /// <summary>
  /// Clears all songs from the table.
  /// </summary>
  public void ClearSongs() {
    _songs.Clear();
    _songTableData.Clear();
    SelectedRow = 0;
    CanFocus = false;
  }

  /// <summary>
  /// Gets the position of the selected row relative to the view's frame.
  /// </summary>
  /// <returns>The point coordinate of the selected row.</returns>
  /// <exception cref="InvalidOperationException">Thrown if no row is selected or visible.</exception>
  public Point GetSelectedRowFramePosition() {
    if (CellToScreen(0, SelectedRow) is { } cellPoint) {
      return new Point(cellPoint.X, cellPoint.Y);
    }

    throw new InvalidOperationException("no row selected or row is not visible");
  }

  /// <summary>
  /// Gets the screen position of the selected row.
  /// </summary>
  /// <returns>The screen coordinate of the selected row.</returns>
  /// <exception cref="InvalidOperationException">Thrown if no row is selected or visible.</exception>
  public Point GetSelectedRowScreenPosition() {
    if (CellToScreen(0, SelectedRow) is { } cellPoint) {
      var tableScreenPos = FrameToScreen();
      var offset = GetAdornmentsThickness();
      return new Point(
          tableScreenPos.X + offset.Left + cellPoint.X,
          tableScreenPos.Y + offset.Top + cellPoint.Y + 1 // Add 1 to account for the header row
      );
    }

    throw new InvalidOperationException("no row selected or row is not visible");
  }

  protected override void OnFrameChanged(in Rectangle frame) {
    base.OnFrameChanged(frame);
    ResizeSongTableColumns(frame);
  }

  private void ResizeSongTableColumns(Rectangle frame) {
    int columnIndex = 0;
    int dynamicColumnCount = 0;
    if (_columns.HasFlag(SongTableColumns.Artist)) dynamicColumnCount++;
    if (_columns.HasFlag(SongTableColumns.Album)) dynamicColumnCount++;
    if (_columns.HasFlag(SongTableColumns.Song)) dynamicColumnCount++;

    // Each column has spacing of 1 pixel between them. Therefore, we need to track that 1 pixel buffer.
    int narrowColumnWidth = this.GetAdornmentsThickness().Horizontal;
    if (_columns.HasFlag(SongTableColumns.Number)) narrowColumnWidth += 3 + 1;
    if (_columns.HasFlag(SongTableColumns.Length)) narrowColumnWidth += 6 + 1;
    if (_columns.HasFlag(SongTableColumns.Year)) narrowColumnWidth += 5 + 1;

    int remainingWidth = (frame.Width - narrowColumnWidth) / dynamicColumnCount - 1;
    int widthRemainer = (frame.Width - narrowColumnWidth) % dynamicColumnCount - 1;

    if (_columns.HasFlag(SongTableColumns.Number)) {
      Style.GetOrCreateColumnStyle(columnIndex).MinWidth = 3;
      Style.GetOrCreateColumnStyle(columnIndex++).MaxWidth = 3;
    }

    if (_columns.HasFlag(SongTableColumns.Artist)) {
      Style.GetOrCreateColumnStyle(columnIndex).MinWidth = remainingWidth;
      Style.GetOrCreateColumnStyle(columnIndex++).MaxWidth = remainingWidth;
    }

    if (_columns.HasFlag(SongTableColumns.Album)) {
      Style.GetOrCreateColumnStyle(columnIndex).MinWidth = remainingWidth;
      Style.GetOrCreateColumnStyle(columnIndex++).MaxWidth = remainingWidth;
    }

    if (_columns.HasFlag(SongTableColumns.Song)) {
      // We expect song names to be both the longest and most important field in the table.
      // Therefore, if there is any remaining, non-evenly divisible width, we place it in the song column.
      Style.GetOrCreateColumnStyle(columnIndex).MinWidth = remainingWidth + widthRemainer;
      Style.GetOrCreateColumnStyle(columnIndex++).MaxWidth = remainingWidth + widthRemainer;
    }

    if (_columns.HasFlag(SongTableColumns.Length)) {
      Style.GetOrCreateColumnStyle(columnIndex).MinWidth = 6;
      Style.GetOrCreateColumnStyle(columnIndex++).MaxWidth = 6;
    }

    if (_columns.HasFlag(SongTableColumns.Year)) {
      Style.GetOrCreateColumnStyle(columnIndex).MinWidth = 5;
      Style.GetOrCreateColumnStyle(columnIndex++).MaxWidth = 5;
    }
  }

  private DataTable CreateDataTable(SongTableColumns columns) {
    var dataTable = new DataTable();
    if (columns.HasFlag(SongTableColumns.Number)) {
      dataTable.Columns.Add("#", typeof(int));
    }

    if (columns.HasFlag(SongTableColumns.Artist)) {
      dataTable.Columns.Add("Artist", typeof(string));
    }

    if (columns.HasFlag(SongTableColumns.Album)) {
      dataTable.Columns.Add("Album", typeof(string));
    }

    if (columns.HasFlag(SongTableColumns.Song)) {
      dataTable.Columns.Add("Track", typeof(string));
    }

    if (columns.HasFlag(SongTableColumns.Length)) {
      dataTable.Columns.Add("Length", typeof(string));
    }

    if (columns.HasFlag(SongTableColumns.Year)) {
      dataTable.Columns.Add("Year", typeof(int));
    }

    return dataTable;
  }
}
