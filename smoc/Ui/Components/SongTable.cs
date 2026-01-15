using System.Data;
using System.Drawing;
using Smoc.Streaming;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.Views;

namespace Smoc.Ui.Components;

public sealed class SongTable : TableView
{
    [Flags]
    public enum SongTableColumns
    {
        Number = 0x1,
        Artist = 0x2,
        Album = 0x4,
        Song = 0x8,
        Length = 0x10,
        Year = 0x20,

        All = Number | Artist | Album | Song | Length | Year
    }

    private readonly DataTable songTableData;
    private readonly SongTableColumns columns;
    private List<Song> songs;
    private int highlightedRow = -1;

    public int HighlightedRow
    {
        get => highlightedRow;
        set
        {
            if (highlightedRow != value)
            {

                highlightedRow = value;
                SetNeedsDraw();
            }
        }
    }

    public event EventHandler<Song>? SongSelected;

    public SongTable(SongTableColumns columns = SongTableColumns.Number | SongTableColumns.Album | SongTableColumns.Song | SongTableColumns.Length | SongTableColumns.Year)
        : base()
    {
        FullRowSelect = true;

        Style.SmoothHorizontalScrolling = true;
        Style.AlwaysShowHeaders = true;
        Style.ShowVerticalCellLines = false;
        Style.ShowVerticalHeaderLines = false;
        Style.ShowHorizontalHeaderOverline = false;
        Style.ShowHorizontalHeaderUnderline = false;
        BorderStyle = LineStyle.Single;

        var highlightedScheme = SchemeManager.GetScheme("TableCurrentTrack");
        var normalScheme = SchemeManager.GetScheme("TableNormalTracks");
        Style.RowColorGetter = (args) => args.RowIndex == highlightedRow ? highlightedScheme : normalScheme;

        this.columns = columns;
        songTableData = CreateDataTable(columns);
        Table = new DataTableSource(songTableData);
        songs = [];
        CanFocus = false;
    }

    public void SetSongs(IEnumerable<Song> songs)
    {
        ClearSongs();
        CanFocus = true;
        this.songs = songs.ToList();
        foreach (var song in this.songs)
        {
            int index = 0;
            var values = new object[songTableData.Columns.Count];
            if (columns.HasFlag(SongTableColumns.Number))
            {
                values[index++] = song.TrackNumber ?? 0;
            }
            if (columns.HasFlag(SongTableColumns.Artist))
            {
                values[index++] = song.Artist.Name;
            }
            if (columns.HasFlag(SongTableColumns.Album))
            {
                values[index++] = song.Album.Name;
            }
            if (columns.HasFlag(SongTableColumns.Song))
            {
                values[index++] = song.Title;
            }
            if (columns.HasFlag(SongTableColumns.Length))
            {
                values[index++] = song.Duration.ToString("m\\:ss");
            }
            if (columns.HasFlag(SongTableColumns.Year))
            {
                values[index++] = song.Album.ReleaseYear ?? 0;
            }
            songTableData.Rows.Add(values);
        }
    }

    protected override bool OnCellActivated(CellActivatedEventArgs args)
    {
        SongSelected?.Invoke(this, songs[SelectedRow]);
        return base.OnCellActivated(args);
    }

    public List<Song> GetSongs()
    {
        return songs.ToList();
    }

    public List<Song> GetSelectedSongAndFollowingSongs()
    {
        return songs.Skip(SelectedRow).ToList();
    }

    public void ClearSongs()
    {
        songs.Clear();
        songTableData.Clear();
        SelectedRow = 0;
        CanFocus = false;
    }

    protected override void OnFrameChanged(in Rectangle frame)
    {
        base.OnFrameChanged(frame);
        ResizeSongTableColumns(frame);
    }

    private void ResizeSongTableColumns(Rectangle frame)
    {
        int columnIndex = 0;
        int dynamicColumnCount = 0;
        if (columns.HasFlag(SongTableColumns.Artist)) dynamicColumnCount++;
        if (columns.HasFlag(SongTableColumns.Album)) dynamicColumnCount++;
        if (columns.HasFlag(SongTableColumns.Song)) dynamicColumnCount++;

        // Each column has spacing of 1 pixel between them. Therefore, we need to track that 1 pixel buffer.
        int narrowColumnWidth = this.GetAdornmentsThickness().Horizontal;
        if (columns.HasFlag(SongTableColumns.Number)) narrowColumnWidth += 3 + 1;
        if (columns.HasFlag(SongTableColumns.Length)) narrowColumnWidth += 6 + 1;
        if (columns.HasFlag(SongTableColumns.Year)) narrowColumnWidth += 5 + 1;

        int remainingWidth = (frame.Width - narrowColumnWidth) / dynamicColumnCount - 1;
        int widthRemainer = (frame.Width - narrowColumnWidth) % dynamicColumnCount - 1;

        if (columns.HasFlag(SongTableColumns.Number))
        {
            Style.GetOrCreateColumnStyle(columnIndex).MinWidth = 3;
            Style.GetOrCreateColumnStyle(columnIndex++).MaxWidth = 3;
        }

        if (columns.HasFlag(SongTableColumns.Artist))
        {
            Style.GetOrCreateColumnStyle(columnIndex).MinWidth = remainingWidth;
            Style.GetOrCreateColumnStyle(columnIndex++).MaxWidth = remainingWidth;
        }

        if (columns.HasFlag(SongTableColumns.Album))
        {
            Style.GetOrCreateColumnStyle(columnIndex).MinWidth = remainingWidth;
            Style.GetOrCreateColumnStyle(columnIndex++).MaxWidth = remainingWidth;
        }

        if (columns.HasFlag(SongTableColumns.Song))
        {
            // We expect song names to be both the longest and most important field in the table.
            // Therefore, if there is any remaining, non-evenly divisible width, we place it in the song column.
            Style.GetOrCreateColumnStyle(columnIndex).MinWidth = remainingWidth + widthRemainer;
            Style.GetOrCreateColumnStyle(columnIndex++).MaxWidth = remainingWidth + widthRemainer;
        }

        if (columns.HasFlag(SongTableColumns.Length))
        {
            Style.GetOrCreateColumnStyle(columnIndex).MinWidth = 6;
            Style.GetOrCreateColumnStyle(columnIndex++).MaxWidth = 6;
        }

        if (columns.HasFlag(SongTableColumns.Year))
        {
            Style.GetOrCreateColumnStyle(columnIndex).MinWidth = 5;
            Style.GetOrCreateColumnStyle(columnIndex++).MaxWidth = 5;
        }
    }

    private DataTable CreateDataTable(SongTableColumns columns)
    {
        var dataTable = new DataTable();
        if (columns.HasFlag(SongTableColumns.Number))
        {
            dataTable.Columns.Add("#", typeof(int));
        }

        if (columns.HasFlag(SongTableColumns.Artist))
        {
            dataTable.Columns.Add("Artist", typeof(string));
        }

        if (columns.HasFlag(SongTableColumns.Album))
        {
            dataTable.Columns.Add("Album", typeof(string));
        }

        if (columns.HasFlag(SongTableColumns.Song))
        {
            dataTable.Columns.Add("Track", typeof(string));
        }

        if (columns.HasFlag(SongTableColumns.Length))
        {
            dataTable.Columns.Add("Length", typeof(string));
        }

        if (columns.HasFlag(SongTableColumns.Year))
        {
            dataTable.Columns.Add("Year", typeof(int));
        }

        return dataTable;
    }
}