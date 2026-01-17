namespace Smoc.Ui;

using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Drawing;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui.Components;
using Smoc.Ui.Models;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

public sealed class ArtistView : View
{
    private static class Messages
    {
        public const string SEARCHING = "searching...";
        public const string LOADING = "loading...";
        public const string NO_ARTISTS_FOUND = "no artists found";
        public const string SELECT_ARTIST = "select an artist";
        public const string NO_SONGS = "no tracks found";
    }

    private CancellationTokenSource? cts;

    private readonly SongTable songTable;
    private readonly SearchResultsList searchResults;
    private readonly Label searchResultsLabel;
    private readonly Label songsLabel;

    private readonly MainWindow mainWindow;
    private readonly IStreamingClient streamingClient;
    private readonly PlayerService playerService;

    public ArtistView(MainWindow mainWindow, CommandService commandService, IStreamingClient streamingClient, PlayerService playerService)
    {
        this.mainWindow = mainWindow;
        this.streamingClient = streamingClient;
        this.playerService = playerService;
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;

        searchResults = new SearchResultsList()
        {
            X = Pos.Absolute(0),
            Y = Pos.Absolute(0),
            Width = Dim.Absolute(30),
            Height = Dim.Fill(),
        };
        searchResults.OpenSelectedItem += OnArtistSelected;
        searchResults.BorderStyle = LineStyle.Single;

        songTable = new SongTable()
        {
            X = Pos.Right(searchResults),
            Y = Pos.Absolute(0),
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        songTable.Style.ShowHeaders = false;
        songTable.SongSelected += OnSongSelected;

        searchResultsLabel = new Label()
        {
            X = Pos.Absolute(1),
            Y = Pos.Center(),
            Width = searchResults.Width - 2,
            Text = Messages.NO_ARTISTS_FOUND,
            TextAlignment = Alignment.Center
        };
        songsLabel = new Label()
        {
            X = Pos.Right(searchResults) + 1,
            Y = Pos.Center(),
            Width = songTable.Width - 2,
            Text = Messages.SELECT_ARTIST,
            TextAlignment = Alignment.Center
        };

        Add(searchResults, songTable, searchResultsLabel, songsLabel);

        commandService.RegisterCommand("a", OnArtistSearchCommand);
    }

    private CancellationToken StartNewOperation()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();
        return cts.Token;
    }

    protected override void Dispose(bool disposing)
    {
        cts?.Cancel();
        cts?.Dispose();
        searchResults.OpenSelectedItem -= OnArtistSelected;
        songTable.SongSelected -= OnSongSelected;
        base.Dispose(disposing);
    }

    private async void OnSongSelected(object? sender, Song e)
    {
        var cancellationToken = StartNewOperation();

        try
        {
            playerService.ClearPlaybackQueue();
            playerService.QueueSongs(songTable.GetSongs());
            await playerService.ChangeTrack(songTable.SelectedRow);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }

    private async void OnArtistSelected(object? sender, ListViewItemEventArgs e)
    {
        var cancellationToken = StartNewOperation();

        try
        {
            if (e.Value is not SearchResultRow<Artist> selectedArtist)
            {
                ResetSongsTable();
                return;
            }

            ResetSongsTable(Messages.LOADING);
            var albums = await streamingClient.GetAlbumsByArtistAsync(selectedArtist.Item);
            if (albums.Count == 0)
            {
                ResetSongsTable(Messages.NO_SONGS);
                return;
            }

            var songTasks = albums.Select(album => streamingClient.GetSongsByAlbumAsync(album));
            var songs = (await Task.WhenAll(songTasks)).SelectMany(s => s);

            if (!songs.Any())
            {
                ResetSongsTable(Messages.NO_SONGS);
                return;
            }

            songTable.SetSongs(songs);
            songTable.Style.ShowHeaders = true;
            songsLabel.Visible = false;
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }

    private async void OnArtistSearchCommand(string command, string args)
    {
        mainWindow.SetMode(Mode.Artist);
        var cancellationToken = StartNewOperation();

        try
        {
            if (args.Length == 0)
            {
                return;
            }

            if (args[0] == '/')
            {
                args = args[1..];
            }

            Logging.Information($"Searching for artist {args}...");

            ResetSongsTable();
            ResetSearchResults();
            var artists = await streamingClient.SearchArtistsAsync(args);

            if (artists.Count == 0)
            {
                ResetSearchResults(Messages.NO_ARTISTS_FOUND);
                return;
            }

            await searchResults.SetSourceAsync(new ObservableCollection<SearchResultRow<Artist>>(artists.Select(artist => new SearchResultRow<Artist>(artist, artist.Name))));
            searchResults.SelectedItem = 0;
            searchResultsLabel.Visible = false;
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }

    private void ResetSearchResults(string message = Messages.SEARCHING)
    {
        searchResultsLabel.Visible = true;
        searchResultsLabel.Text = message;
        searchResults.SelectedItem = null;
        searchResults.Source = null;
    }

    private void ResetSongsTable(string message = Messages.SELECT_ARTIST)
    {
        songsLabel.Visible = true;
        songsLabel.Text = message;
        songTable.Style.ShowHeaders = false;
        songTable.ClearSongs();
    }
}