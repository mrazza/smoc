using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui.Components;
using Smoc.Ui.Models;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using System.Drawing;
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
    private readonly SongContextMenu songContextMenu;
    private readonly Label songsLabel;
    private readonly IStreamingClient streamingClient;
    private readonly PlayerService playerService;

    private CancellationTokenSource? searchCts;

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
        songContextMenu = new SongContextMenu(playerService, songTable);

        Add(songTable, songsLabel, songContextMenu);

        commandService.RegisterCommand("t", OnTrackSearchCommand);
    }

    protected override void Dispose(bool disposing)
    {
        CancelPendingSearches();
        songTable.SongSelected -= OnSongSelected;
        base.Dispose(disposing);
    }

    private void OnSongSelected(object? sender, List<Song> songs)
    {
        var tableAdornments = songTable.GetAdornmentsThickness();
        var yPos = songTable.SelectedRow + tableAdornments.Top + 2;
        int menuHeight = songContextMenu.RequiredHeight;

        if (yPos + menuHeight > Frame.Height)
        {
            // Not enough space below the row, so put it above
            // Let's position it so the bottom of the menu ends at p.Y
            yPos = yPos - menuHeight - 1;
        }

        songContextMenu.MakeVisible(new Point(songTable.Frame.X + tableAdornments.Left, yPos));
        songContextMenu.SetFocus();
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