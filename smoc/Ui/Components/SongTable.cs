using System.CommandLine;
using System.Data;
using System.Drawing;
using System.Text.Json.Serialization;
using Microsoft.VisualBasic;
using Smoc.Streaming;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace Smoc.Ui.Components;

/// <summary>
/// A table view for displaying a list of songs with configurable columns.
/// </summary>
public class SongTable : TableView {
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
  /// Gets or sets the 0-based index of the currently selected row.
  /// </summary>
  public int SelectedRow {
    get => Value?.Cursor.Y ?? -1;
    set {
      if (value != Value?.Cursor.Y) {
        SetSelection(0, value, false);
      }
    }
  }

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

    Scheme? highlightedScheme = null;
    Scheme? normalScheme = null;
    try {
      highlightedScheme = SchemeManager.GetScheme("TableCurrentTrack");
      normalScheme = SchemeManager.GetScheme("TableNormalTracks");
    } catch (KeyNotFoundException) {
      Logging.Error("SchemeManager.GetScheme() failed to find required schemes");
    }
    highlightedScheme ??= new Scheme(new Attribute(Color.White, Color.Black, TextStyle.Bold));
    normalScheme ??= SchemeManager.GetScheme(Schemes.Base);
    Style.RowColorGetter = (args) => args.RowIndex == _highlightedRow ? highlightedScheme : normalScheme;

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

  /// <inheritdoc />
  protected override bool OnAccepting(CommandEventArgs args) {
    SongSelected?.Invoke(this, GetSelectedSongs());
    return true;
  }

  /// <inheritdoc />
  protected override bool OnKeyDownNotHandled(Key key) {
    base.OnKeyDownNotHandled(key);
    return false;
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
    RefreshContentSize();
  }

  /// <inheritdoc />
  protected override void OnHasFocusChanged(bool newHasFocus, View? previousFocusedView, View? focusedView) {
    base.OnHasFocusChanged(newHasFocus, previousFocusedView, focusedView);

    if (newHasFocus && SelectedRow == -1 && _songTableData.Rows.Count > 0) {
      SelectedRow = 0;
    }
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
        .FirstOrDefault()?.Select(index => _songs[index]).ToList() ?? (SelectedRow >= 0 ? [_songs[SelectedRow]] : []);
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
  /// Gets the screen position of the selected row.
  /// </summary>
  /// <returns>The screen coordinate of the selected row.</returns>
  /// <exception cref="InvalidOperationException">Thrown if no row is selected or visible.</exception>
  public Point GetSelectedRowScreenPosition() {
    return RowToScreen(SelectedRow) ?? throw new InvalidOperationException("no row selected or row is not visible");
  }

  /// <summary>
  /// Gets the screen position of the selected row.
  /// </summary>
  /// <returns>The screen coordinate of the selected row.</returns>
  /// <exception cref="InvalidOperationException">Thrown if no row is selected or visible.</exception>
  public Point GetSelectedRowFramePosition() {
    var rowToScreen = RowToScreen(SelectedRow) ?? throw new InvalidOperationException("no row selected or row is not visible");
    var adornmentThickness = GetAdornmentsThickness();
    var frameOffset = FrameToScreen().Location;
    return new Point(rowToScreen.X - frameOffset.X - adornmentThickness.Left, rowToScreen.Y - frameOffset.Y - adornmentThickness.Top - 1);
  }

  protected override void OnFrameChanged(in Rectangle frame) {
    ResizeSongTableColumns(frame);
    base.OnFrameChanged(frame);
  }

  private Point? RowToScreen(int tableRow) {
    return ContentToScreen(new Point(0, tableRow + 1 + GetAdornmentsThickness().Top));
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

    RefreshContentSize();
  }

  private static DataTable CreateDataTable(SongTableColumns columns) {
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
