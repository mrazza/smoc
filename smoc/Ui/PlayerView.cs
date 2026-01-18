namespace Smoc.Ui;

using System;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui.Components;
using Smoc.Ui.Models;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using static Smoc.Ui.Components.SongTable;

public sealed class PlayerView : View
{
    private readonly SongTable songTable;
    private readonly Label noSongsLabel;
    private readonly PlayerService playerService;
    private readonly MainWindow mainWindow;

    public PlayerView(MainWindow mainWindow, CommandService commandService, PlayerService playerService)
    {
        this.mainWindow = mainWindow;
        this.playerService = playerService;
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;
        noSongsLabel = new Label()
        {
            X = Pos.Center(),
            Y = Pos.Center(),
            Text = "queue is empty"
        };
        songTable = new SongTable(SongTableColumns.Artist | SongTableColumns.Album | SongTableColumns.Song | SongTableColumns.Length | SongTableColumns.Year)
        {
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        songTable.Style.ShowHeaders = false;
        songTable.BorderStyle = LineStyle.Single;

        Add(songTable, noSongsLabel);

        commandService.RegisterCommand("np", OnNowPlayingCommand);

        playerService.QueueChanged += OnQueueChanged;
        playerService.SongChanged += OnSongChanged;

        songTable.SongSelected += OnSongSelected;
    }

    protected override void Dispose(bool disposing)
    {
        playerService.QueueChanged -= OnQueueChanged;
        playerService.SongChanged -= OnSongChanged;
        songTable.SongSelected -= OnSongSelected;
        base.Dispose(disposing);
    }

    protected override void OnVisibleChanged()
    {
        base.OnVisibleChanged();

        if (Visible && playerService.CurrentSong is Song currentSong)
        {
            songTable.SelectedRow = playerService.CurrentPlaybackIndex;
            songTable.EnsureSelectedCellIsVisible();
        }
    }

    private async void OnSongSelected(object? sender, Song e)
    {
        await playerService.ChangeTrack(songTable.SelectedRow);
    }

    private void OnSongChanged(object? sender, Song e)
    {
        songTable.HighlightedRow = playerService.CurrentPlaybackIndex;
    }

    private void OnQueueChanged(object? sender, EventArgs e)
    {
        var queue = playerService.GetCurrentPlaybackQueue();
        if (!queue.Any())
        {
            noSongsLabel.Visible = true;
            songTable.Style.ShowHeaders = false;
        }
        else
        {
            noSongsLabel.Visible = false;
            songTable.Style.ShowHeaders = true;
            songTable.SetSongs(queue);
            songTable.SelectedRow = playerService.CurrentPlaybackIndex;
        }
    }

    private void OnNowPlayingCommand(string command, string args)
    {
        mainWindow.SetMode(Mode.Player);
    }
}