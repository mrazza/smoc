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
        public const string SEARCH_ERROR = "error searching artists";
        public const string SONG_LOAD_ERROR = "error loading tracks";
    }

    private readonly SongTable songTable;
    private readonly SearchResultsList searchResults;
    private readonly Label searchResultsLabel;
    private readonly Label songsLabel;

    private readonly MainWindow mainWindow;
    private readonly IStreamingClient streamingClient;
    private readonly PlayerService playerService;

    private CancellationTokenSource? searchCts;
    private CancellationTokenSource? selectArtistCts;
    private PopoverMenu? songActionPopover;

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

    protected override void Dispose(bool disposing)
    {
        CancelPendingSearches();
        searchResults.OpenSelectedItem -= OnArtistSelected;
        songTable.SongSelected -= OnSongSelected;
        songActionPopover?.Dispose();
        base.Dispose(disposing);
    }

    private void OnSongSelected(object? sender, Song e)
    {
        var selectedSong = e;
        var selectedIndex = songTable.SelectedRow;
        var allSongs = songTable.GetSongs();

        if (songActionPopover is null)
        {
            songActionPopover = new PopoverMenu(
            [
                new MenuItem { Title = "_Play All from Here" },
                new MenuItem { Title = "Play _Only This" },
                new MenuItem { Title = "Play _Next" },
                new MenuItem { Title = "_Add to Queue" }
            ]);
            App!.Popover?.Register(songActionPopover);
        }

        // Always update the menu item actions with current song context
        var menuItems = songActionPopover.Root!.SubViews.OfType<MenuItem>().ToList();
        if (menuItems.Count >= 4)
        {
            menuItems[0].Action = async () =>
            {
                playerService.ClearPlaybackQueue();
                playerService.QueueSongs(allSongs);
                await playerService.ChangeTrack(selectedIndex);
            };
            menuItems[1].Action = async () =>
            {
                playerService.ClearPlaybackQueue();
                playerService.QueueSong(selectedSong);
                await playerService.ChangeTrack(0);
            };
            menuItems[2].Action = () => playerService.InsertAfterCurrent(selectedSong);
            menuItems[3].Action = () => playerService.QueueSong(selectedSong);
        }

        var position = new Point(
            songTable.FrameToScreen().X,
            songTable.FrameToScreen().Y + songTable.SelectedRow + 1);
        songActionPopover.MakeVisible(position);
    }

    private async void OnArtistSelected(object? sender, ListViewItemEventArgs e)
    {
        if (e.Value is not SearchResultRow<Artist> selectedArtist)
        {
            ResetSongsTable();
            return;
        }

        CancelPendingSearches();
        selectArtistCts = new CancellationTokenSource();
        var token = selectArtistCts.Token;

        try
        {
            ResetSongsTable(Messages.LOADING);
            Logging.Information($"Loading artist {selectedArtist.Item.Name}...");
            var albums = await streamingClient.GetAlbumsByArtistAsync(selectedArtist.Item, token);

            if (token.IsCancellationRequested) return;

            if (albums.Count == 0)
            {
                ResetSongsTable(Messages.NO_SONGS);
                return;
            }

            // Note: Parallel fetch is okay, but we should pass token. 
            // If one fails/cancels, we bail.
            var songTasks = albums.Select(album => streamingClient.GetSongsByAlbumAsync(album, token));
            var songs = (await Task.WhenAll(songTasks)).SelectMany(s => s);

            if (token.IsCancellationRequested) return;

            Logging.Information($"Loaded {songs.Count()} songs for artist {selectedArtist.Item.Name}.");

            if (!songs.Any())
            {
                ResetSongsTable(Messages.NO_SONGS);
                return;
            }

            songTable.SetSongs(songs);
            songTable.Style.ShowHeaders = true;
            songsLabel.Visible = false;
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        catch (Exception ex)
        {
            Logging.Error($"Error loading artist details: {ex.Message}");
            mainWindow.DisplayError(Messages.SONG_LOAD_ERROR);
            ResetSongsTable(Messages.SONG_LOAD_ERROR);
        }
    }

    private async void OnArtistSearchCommand(string command, string args)
    {
        mainWindow.SetMode(Mode.Artist);

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

        try
        {
            Logging.Information($"Searching for artist {args}...");

            ResetSongsTable();
            ResetSearchResults();
            var artists = await streamingClient.SearchArtistsAsync(args, token);

            if (token.IsCancellationRequested) return;

            Logging.Information($"Found {artists.Count} artists for search '{args}'.");

            if (artists.Count == 0)
            {
                ResetSearchResults(Messages.NO_ARTISTS_FOUND);
                return;
            }

            await searchResults.SetSourceAsync(new ObservableCollection<SearchResultRow<Artist>>(artists.Select(artist => new SearchResultRow<Artist>(artist, artist.Name))));
            searchResults.SelectedItem = 0;
            searchResultsLabel.Visible = false;
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            Logging.Error($"Error searching artists: {ex.Message}");
            mainWindow.DisplayError(Messages.SEARCH_ERROR);
            ResetSearchResults(Messages.SEARCH_ERROR);
        }
    }

    private void CancelPendingSearches()
    {
        searchCts?.Cancel();
        searchCts?.Dispose();
        searchCts = null;
        selectArtistCts?.Cancel();
        selectArtistCts?.Dispose();
        selectArtistCts = null;
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