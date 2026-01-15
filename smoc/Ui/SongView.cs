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
    }

    private readonly MainWindow mainWindow;
    private readonly SongTable songTable;
    private readonly Label songsLabel;
    private readonly IStreamingClient streamingClient;

    public SongView(MainWindow mainWindow, CommandService commandService, IStreamingClient streamingClient)
    {
        this.mainWindow = mainWindow;
        this.streamingClient = streamingClient;

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
        songTable.SongSelected -= OnSongSelected;
        base.Dispose(disposing);
    }

    private void OnSongSelected(object? sender, Song e)
    {
        mainWindow.DisplayError("playing from song view not implemented.");
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

        ResetTable();

        Logging.Information($"Searching for track {args}...");
        var songs = await streamingClient.SearchSongsAsync(args);
        if (songs.Count == 0)
        {
            ResetTable(Messages.NO_SONGS);
            return;
        }

        songTable.Style.ShowHeaders = true;
        songsLabel.Visible = false;
        songTable.SetSongs(songs);
    }

    private void ResetTable(string message = Messages.SEARCHING)
    {
        songsLabel.Visible = true;
        songsLabel.Text = message;
        songTable.Style.ShowHeaders = false;
        songTable.ClearSongs();
    }
}