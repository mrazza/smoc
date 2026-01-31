using System.Collections.ObjectModel;
using System.Drawing;
using Smoc.Services;
using Smoc.Services.Util;
using Smoc.Streaming;
using Smoc.Ui.Components;
using Smoc.Ui.Models;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using static Smoc.Ui.Components.SongTable;

namespace Smoc.Ui;

/// <summary>
/// A view for displaying and interacting with playlists; including some default playlists like liked songs.
/// </summary>
public class PlaylistView : View {
  private static class Messages {
    public const string SEARCHING = "searching...";
    public const string NO_PLAYLISTS_FOUND = "no playlists found";
    public const string NO_SONGS = "no tracks";
    public const string SELECT_PLAYLIST = "select a playlist";
    public const string LOADING_SONGS = "loading tracks...";
    public const string SONG_LOAD_ERROR = "error loading tracks";
    public const string SEARCH_ERROR = "error searching playlists";
  }

  private readonly IMainWindow _mainWindow;
  private readonly SongTable _songTable;
  private readonly Label _songsLabel;
  private readonly SongContextMenu _songContextMenu;
  private readonly Label _searchResultsLabel;
  private readonly SearchResultsList<SearchResultRow<Playlist>> _searchResults;
  private readonly UniqueResource<CancellationTokenSource> _loadPlaylistCtsResource;
  private readonly UniqueResource<CancellationTokenSource> _searchCtsResource;
  private readonly IStreamingClient _streamingClient;

  public PlaylistView(IMainWindow mainWindow, CommandService commandService, IPlaybackQueueService playbackQueueService, IStreamingClient streamingClient) {
    _mainWindow = mainWindow;
    _streamingClient = streamingClient;
    _loadPlaylistCtsResource = new UniqueResource<CancellationTokenSource>((source) => source.Cancel());
    _searchCtsResource = new UniqueResource<CancellationTokenSource>((source) => source.Cancel());

    Width = Dim.Fill();
    Height = Dim.Fill();
    CanFocus = true;

    _searchResults = new SearchResultsList<SearchResultRow<Playlist>>() {
      X = Pos.Absolute(0),
      Y = Pos.Absolute(0),
      Width = Dim.Absolute(30),
      Height = Dim.Fill(),
    };
    _searchResults.BorderStyle = LineStyle.Single;
    _searchResults.SearchResultSelected += OnPlaylistSelected;
    _searchResultsLabel = new Label() {
      X = Pos.Absolute(1),
      Y = Pos.Center(),
      Width = _searchResults.Width - 2,
      Text = Messages.NO_PLAYLISTS_FOUND,
      TextAlignment = Alignment.Center
    };

    _songTable = new SongTable(SongTableColumns.Artist | SongTableColumns.Album | SongTableColumns.Song | SongTableColumns.Length) {
      X = Pos.Right(_searchResults),
      Width = Dim.Fill(),
      Height = Dim.Fill()
    };
    _songTable.Style.ShowHeaders = false;
    _songTable.BorderStyle = LineStyle.Single;
    _songTable.SongSelected += OnSongSelected;
    _songsLabel = new Label() {
      X = Pos.Right(_searchResults) + 1,
      Y = Pos.Center(),
      Width = _songTable.Width - 2,
      TextAlignment = Alignment.Center
    };

    _songContextMenu = new SongContextMenu(playbackQueueService, _songTable);


    ResetSongsTable();

    Add(_searchResults, _searchResultsLabel, _songTable, _songsLabel, _songContextMenu);

    commandService.RegisterCommand("p", OnPlaylistCommand);
    commandService.RegisterCommand("likes", OnLikedPlaylistCommand);
    commandService.RegisterCommand("url", OnUrlPlaylistCommand);
  }

  private async void OnUrlPlaylistCommand(string command, string args) {
    _mainWindow.SetMode(Mode.Playlist);

    if (args.Length == 0) {
      return;
    }

    HideSearchResults();
    ResetSongsTable(Messages.LOADING_SONGS);

    _searchCtsResource.Resource?.Cancel();
    var token = _loadPlaylistCtsResource.Replace(new CancellationTokenSource()).Token;

    try {
      var songs = await _streamingClient.GetPlaylistSongsFromUrlAsync(args, token);

      token.ThrowIfCancellationRequested();

      if (songs.Count == 0) {
        ResetSongsTable();
        return;
      }

      _songTable.SetSongs(songs);
      _songTable.Style.ShowHeaders = true;
      _songsLabel.Visible = false;
    } catch (OperationCanceledException) {
      Logging.Debug("Loading url playlist cancelled");
    } catch (Exception ex) {
      ResetSongsTable(Messages.SONG_LOAD_ERROR);
      Logging.Error($"Error loading url: {ex.Message}");
      _mainWindow.DisplayError("error loading url");
    }
  }

  private void OnSongSelected(object? sender, List<Song> songs) {
    _songContextMenu.MakeVisibleInView(this);
    _songContextMenu.SetFocus();
  }

  private async void OnLikedPlaylistCommand(string command, string args) {
    _mainWindow.SetMode(Mode.Playlist);
    HideSearchResults();
    ResetSongsTable(Messages.LOADING_SONGS);

    _searchCtsResource.Resource?.Cancel();
    var token = _loadPlaylistCtsResource.Replace(new CancellationTokenSource()).Token;

    try {
      var songs = await _streamingClient.GetLikedSongsAsync(token);

      token.ThrowIfCancellationRequested();

      if (songs.Count == 0) {
        ResetSongsTable();
        return;
      }

      _songTable.SetSongs(songs);
      _songTable.Style.ShowHeaders = true;
      _songsLabel.Visible = false;
    } catch (OperationCanceledException) {
      Logging.Debug("Loading liked playlist cancelled");
    } catch (Exception ex) {
      ResetSongsTable(Messages.SONG_LOAD_ERROR);
      Logging.Error($"Error loading liked playlist: {ex.Message}");
      _mainWindow.DisplayError("error loading liked playlist");
    }
  }

  private async void OnPlaylistCommand(string command, string args) {
    _mainWindow.SetMode(Mode.Playlist);

    if (args.Length == 0) {
      return;
    }

    ShowSearchResults();
    ResetSearchResults();
    ResetSongsTable(Messages.SELECT_PLAYLIST);

    _loadPlaylistCtsResource.Resource?.Cancel();
    var token = _searchCtsResource.Replace(new CancellationTokenSource()).Token;

    try {
      Logging.Information($"Searching for playlist {args}...");

      var playlists = await _streamingClient.SearchPlaylistsAsync(args, token);

      token.ThrowIfCancellationRequested();

      Logging.Information($"Found {playlists.Count} playlists for search '{args}'.");

      if (playlists.Count == 0) {
        ResetSearchResults(Messages.NO_PLAYLISTS_FOUND);
        return;
      }

      _searchResults.SetSource(new ObservableCollection<SearchResultRow<Playlist>>(playlists.Select(playlist => new SearchResultRow<Playlist>(playlist, playlist.Name))));
      _searchResults.SelectedItem = 0;
      _searchResultsLabel.Visible = false;
    } catch (OperationCanceledException) {
      // Ignore
      Logging.Debug("Playlist search cancelled");
    } catch (Exception ex) {
      Logging.Error($"Error searching playlists: {ex.Message}");
      _mainWindow.DisplayError(Messages.SEARCH_ERROR);
      ResetSearchResults(Messages.SEARCH_ERROR);
    }
  }

  private async void OnPlaylistSelected(object? sender, SearchResultRow<Playlist> selectedPlaylist) {
    _searchCtsResource.Resource?.Cancel();
    var token = _loadPlaylistCtsResource.Replace(new CancellationTokenSource()).Token;
    ResetSongsTable(Messages.LOADING_SONGS);

    try {
      Logging.Information($"Loading playlist {selectedPlaylist.Item.Name}...");
      var songs = await _streamingClient.GetPlaylistSongsAsync(selectedPlaylist.Item, token);

      token.ThrowIfCancellationRequested();

      Logging.Information($"Loaded {songs.Count} songs for playlist {selectedPlaylist.Item.Name}.");

      if (songs.Count == 0) {
        ResetSongsTable(Messages.NO_SONGS);
        return;
      }

      _songTable.SetSongs(songs);
      _songTable.Style.ShowHeaders = true;
      _songsLabel.Visible = false;
    } catch (OperationCanceledException) {
      // Ignore
      Logging.Debug("Playlist load cancelled");
    } catch (Exception ex) {
      Logging.Error($"Error loading playlist: {ex.Message}");
      _mainWindow.DisplayError(Messages.SONG_LOAD_ERROR);
      ResetSongsTable(Messages.SONG_LOAD_ERROR);
    }
  }

  private void HideSearchResults() {
    if (!_searchResults.Visible) return;
    _searchResults.Visible = false;
    _searchResults.Width = Dim.Absolute(0);
    _searchResultsLabel.Visible = false;
  }

  private void ShowSearchResults() {
    if (_searchResults.Visible) return;
    _searchResults.Visible = true;
    _searchResults.Width = Dim.Absolute(30);
    _searchResultsLabel.Visible = true;
  }

  private void ResetSearchResults(string message = Messages.SEARCHING) {
    _searchResultsLabel.Visible = true;
    _searchResultsLabel.Text = message;
    _searchResults.SelectedItem = null;
    _searchResults.Source = null;
  }

  private void ResetSongsTable(string message = Messages.NO_SONGS) {
    _songsLabel.Visible = true;
    _songsLabel.Text = message;
    _songTable.Style.ShowHeaders = false;
    _songTable.ClearSongs();
  }
}