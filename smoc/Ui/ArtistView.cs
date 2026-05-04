namespace Smoc.Ui;

using System;
using System.Collections.ObjectModel;
using System.Data;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui.Components;
using Smoc.Ui.Models;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

/// <summary>
/// The primary view for the ARTIST mode.
/// It displays a list of artists and allows the user to search for artists.
/// </summary>
public sealed class ArtistView : View {
  /// <summary>
  /// Human-readable messages used in the view.
  /// </summary>
  private static class Messages {
    public const string SEARCHING = "searching...";
    public const string LOADING = "loading...";
    public const string NO_ARTISTS_FOUND = "no artists found";
    public const string SELECT_ARTIST = "select an artist";
    public const string NO_SONGS = "no tracks found";
    public const string SEARCH_ERROR = "error searching artists";
    public const string SONG_LOAD_ERROR = "error loading tracks";
  }

  private readonly SongTable _songTable;
  private readonly SongContextMenu _songContextMenu;
  private readonly SearchResultsList<SearchResultRow<Artist>> _searchResults;
  private readonly Label _searchResultsLabel;
  private readonly Label _songsLabel;

  private readonly IMainWindow _mainWindow;
  private readonly CommandService _commandService;
  private readonly IStreamingClient _streamingClient;

  private CancellationTokenSource? _searchCts;
  private CancellationTokenSource? _selectArtistCts;

  /// <summary>
  /// Initializes a new instance of the <see cref="ArtistView"/> class.
  /// </summary>
  /// <param name="mainWindow">The main window reference.</param>
  /// <param name="commandService">The command service for registering search commands.</param>
  /// <param name="streamingClient">The client for fetching artist data.</param>
  /// <param name="playbackQueueService">The playback queue service for playback options.</param>
  public ArtistView(IMainWindow mainWindow, CommandService commandService, IStreamingClient streamingClient, IPlaybackQueueService playbackQueueService) {
    _mainWindow = mainWindow;
    _commandService = commandService;
    _streamingClient = streamingClient;
    Width = Dim.Fill();
    Height = Dim.Fill();
    CanFocus = true;

    _searchResults = new SearchResultsList<SearchResultRow<Artist>>() {
      X = Pos.Absolute(0),
      Y = Pos.Absolute(0),
      Width = Dim.Absolute(30),
      Height = Dim.Fill()
    };
    _searchResults.SearchResultSelected += OnArtistSelected;
    _searchResults.BorderStyle = LineStyle.Single;

    _songTable = new SongTable() {
      X = Pos.Right(_searchResults),
      Y = Pos.Absolute(0),
      Width = Dim.Fill(),
      Height = Dim.Fill()
    };
    _songTable.Style.ShowHeaders = false;
    _songTable.SongSelected += OnSongSelected;
    _songContextMenu = new SongContextMenu(playbackQueueService, _songTable);

    _searchResultsLabel = new Label() {
      X = Pos.Absolute(1),
      Y = Pos.Center(),
      Width = _searchResults.Width - 2,
      Text = Messages.NO_ARTISTS_FOUND,
      TextAlignment = Alignment.Center
    };
    _songsLabel = new Label() {
      X = Pos.Right(_searchResults) + 1,
      Y = Pos.Center(),
      Width = _songTable.Width - 2,
      Text = Messages.SELECT_ARTIST,
      TextAlignment = Alignment.Center
    };

    Add(_searchResults, _songTable, _searchResultsLabel, _songsLabel, _songContextMenu);

    _commandService.RegisterCommand("a", OnArtistSearchCommand);
  }

  protected override void Dispose(bool disposing) {
    CancelPendingSearches();
    _searchResults.SearchResultSelected -= OnArtistSelected;
    _songTable.SongSelected -= OnSongSelected;
    _commandService.UnregisterCommand("a");
    base.Dispose(disposing);
  }

  private void OnSongSelected(object? sender, List<Song> songs) {
    _songContextMenu.MakeVisibleInView(this);
    _songContextMenu.SetFocus();
  }

  private async void OnArtistSelected(object? sender, SearchResultRow<Artist> selectedArtist) {
    CancelPendingSearches();
    _selectArtistCts = new CancellationTokenSource();
    var token = _selectArtistCts.Token;

    try {
      ResetSongsTable(Messages.LOADING);
      Logging.Information($"Loading artist {selectedArtist.Item.Name}...");
      var albums = await _streamingClient.GetAlbumsByArtistAsync(selectedArtist.Item, token);

      if (token.IsCancellationRequested) return;

      if (albums.Count == 0) {
        ResetSongsTable(Messages.NO_SONGS);
        return;
      }

      // Note: Parallel fetch is okay, but we should pass token. 
      // If one fails/cancels, we bail.
      var songTasks = albums.Select(album => _streamingClient.GetSongsByAlbumAsync(album, token));
      var songs = (await Task.WhenAll(songTasks)).SelectMany(s => s);

      if (token.IsCancellationRequested) return;

      Logging.Information($"Loaded {songs.Count()} songs for artist {selectedArtist.Item.Name}.");

      if (!songs.Any()) {
        ResetSongsTable(Messages.NO_SONGS);
        return;
      }

      _songTable.SetSongs(songs);
      _songTable.SelectedRow = 0;
      _songTable.EnsureCursorIsVisible();
      _songTable.Style.ShowHeaders = true;
      _songsLabel.Visible = false;
    } catch (OperationCanceledException) {
      // Ignore cancellation
    } catch (Exception ex) {
      Logging.Error($"Error loading artist details: {ex.Message}");
      _mainWindow.DisplayError(Messages.SONG_LOAD_ERROR);
      ResetSongsTable(Messages.SONG_LOAD_ERROR);
    }
  }

  private async void OnArtistSearchCommand(string command, string args) {
    _mainWindow.SetMode(Mode.Artist);

    if (args.Length == 0) {
      return;
    }

    CancelPendingSearches();
    _searchCts = new CancellationTokenSource();
    var token = _searchCts.Token;

    try {
      Logging.Information($"Searching for artist {args}...");

      ResetSongsTable();
      ResetSearchResults();
      var artists = await _streamingClient.SearchArtistsAsync(args, token);

      if (token.IsCancellationRequested) return;

      Logging.Information($"Found {artists.Count} artists for search '{args}'.");

      if (artists.Count == 0) {
        ResetSearchResults(Messages.NO_ARTISTS_FOUND);
        return;
      }

      _searchResults.SetSource(new ObservableCollection<SearchResultRow<Artist>>(artists.Select(artist => new SearchResultRow<Artist>(artist, artist.Name))));
      _searchResults.SelectedItem = 0;
      _searchResultsLabel.Visible = false;
    } catch (OperationCanceledException) {
      // Ignore
      Logging.Debug("Artist search cancelled");
    } catch (Exception ex) {
      Logging.Error($"Error searching artists: {ex.Message}");
      _mainWindow.DisplayError(Messages.SEARCH_ERROR);
      ResetSearchResults(Messages.SEARCH_ERROR);
    }
  }

  private void CancelPendingSearches() {
    _searchCts?.Cancel();
    _searchCts?.Dispose();
    _searchCts = null;
    _selectArtistCts?.Cancel();
    _selectArtistCts?.Dispose();
    _selectArtistCts = null;
  }

  private void ResetSearchResults(string message = Messages.SEARCHING) {
    _searchResultsLabel.Visible = true;
    _searchResultsLabel.Text = message;
    _searchResults.SelectedItem = null;
    _searchResults.Source = null;
  }

  private void ResetSongsTable(string message = Messages.SELECT_ARTIST) {
    _songsLabel.Visible = true;
    _songsLabel.Text = message;
    _songTable.Style.ShowHeaders = false;
    _songTable.ClearSongs();
  }
}
