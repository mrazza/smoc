using System.Drawing;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui.Components;
using Smoc.Ui.Models;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using static Smoc.Ui.Components.SongTable;

namespace Smoc.Ui;

public sealed class SongView : View
{
    private static class Messages
    {
        public const string SEARCHING = "searching...";
        public const string NO_SONGS = "no tracks found";
        public const string SEARCH_ERROR = "error searching tracks";
    }

    private readonly MainWindow mainWindow;
    private readonly SongTable songTable;
    private readonly Label songsLabel;
    private readonly IStreamingClient streamingClient;
    private readonly PlayerService playerService;

    private CancellationTokenSource? searchCts;
    private SongActionPopoverHelper? songActionPopoverHelper;

    public SongView(MainWindow mainWindow, CommandService commandService, IStreamingClient streamingClient, PlayerService playerService)
    {
        this.mainWindow = mainWindow;
        this.streamingClient = streamingClient;
        this.playerService = playerService;

        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;

        songsLabel = new Label()
        {
            X = Pos.Center(),
            Y = Pos.Center(),
            Text = Messages.SEARCHING
        };
        songTable = new SongTable(SongTableColumns.Artist | SongTableColumns.Album | SongTableColumns.Song | SongTableColumns.Length)
        {
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        songTable.Style.ShowHeaders = false;
        songTable.BorderStyle = LineStyle.Single;
        songTable.SongSelected += OnSongSelected;

        Add(songTable, songsLabel);

        commandService.RegisterCommand("t", OnTrackSearchCommand);
    }

    protected override void Dispose(bool disposing)
    {
        CancelPendingSearches();
        songTable.SongSelected -= OnSongSelected;
        songActionPopoverHelper?.Dispose();
        base.Dispose(disposing);
    }

    private void OnSongSelected(object? sender, Song e)
    {
        songActionPopoverHelper ??= new SongActionPopoverHelper(playerService, this);

        var selectedSong = e;
        var selectedIndex = songTable.SelectedRow;
        var allSongs = songTable.GetSongs();

        var position = new Point(
            songTable.FrameToScreen().X,
            songTable.FrameToScreen().Y + songTable.SelectedRow + 1);

        songActionPopoverHelper.Show(selectedSong, allSongs, selectedIndex, position);
    }

    private async void OnTrackSearchCommand(string command, string args)
    {
        mainWindow.SetMode(Mode.Song);

        if (args.Length == 0)
        {
            return;
        }

        if (args[0] == '/')
        {
            args = args[1..];
        }

        CancelPendingSearches();
        searchCts = new CancellationTokenSource();
        var token = searchCts.Token;

        ResetTable();

        try
        {
            Logging.Information($"Searching for track {args}...");
            var songs = await streamingClient.SearchSongsAsync(args, token);

            if (token.IsCancellationRequested) return;

            Logging.Information($"Found {songs.Count} tracks for search '{args}'.");

            if (songs.Count == 0)
            {
                ResetTable(Messages.NO_SONGS);
                return;
            }

            songTable.Style.ShowHeaders = true;
            songsLabel.Visible = false;
            songTable.SetSongs(songs);
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            Logging.Error($"Error searching tracks: {ex.Message}");
            mainWindow.DisplayError(Messages.SEARCH_ERROR);
            ResetTable(Messages.SEARCH_ERROR);
        }
    }

    private void CancelPendingSearches()
    {
        searchCts?.Cancel();
        searchCts?.Dispose();
        searchCts = null;
    }

    private void ResetTable(string message = Messages.SEARCHING)
    {
        songsLabel.Visible = true;
        songsLabel.Text = message;
        songTable.Style.ShowHeaders = false;
        songTable.ClearSongs();
    }
}