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
    public const string NO_SONGS = "no tracks";
    public const string LOADING_SONGS = "loading tracks...";
    public const string SONG_LOAD_ERROR = "error loading tracks";
  }

  private readonly IMainWindow _mainWindow;
  private readonly SongTable _songTable;
  private readonly Label _songsLabel;
  private readonly SongContextMenu _songContextMenu;
  private readonly UniqueResource<CancellationTokenSource> _loadPlaylistCtsResource;
  private readonly IStreamingClient _streamingClient;

  public PlaylistView(IMainWindow mainWindow, CommandService commandService, IPlaybackQueueService playbackQueueService, IStreamingClient streamingClient) {
    _mainWindow = mainWindow;
    _streamingClient = streamingClient;
    _loadPlaylistCtsResource = new UniqueResource<CancellationTokenSource>((source) => source.Cancel());

    Width = Dim.Fill();
    Height = Dim.Fill();
    CanFocus = true;

    _songsLabel = new Label() {
      X = Pos.Center(),
      Y = Pos.Center()
    };
    _songTable = new SongTable(SongTableColumns.Artist | SongTableColumns.Album | SongTableColumns.Song | SongTableColumns.Length) {
      Width = Dim.Fill(),
      Height = Dim.Fill()
    };
    _songTable.Style.ShowHeaders = false;
    _songTable.BorderStyle = LineStyle.Single;
    _songTable.SongSelected += OnSongSelected;
    _songContextMenu = new SongContextMenu(playbackQueueService, _songTable);

    ResetSongsTable();

    Add(_songTable, _songsLabel, _songContextMenu);

    commandService.RegisterCommand("p", OnPlaylistCommand);
    commandService.RegisterCommand("likes", OnLikedPlaylistCommand);
  }

  private void OnSongSelected(object? sender, List<Song> songs) {
    // TODO(razza): Extract this for reuse in SongView and ArtistView
    var tableAdornments = _songTable.GetAdornmentsThickness();
    var yPos = _songTable.GetSelectedRowFramePosition().Y + tableAdornments.Top + 1;
    int menuHeight = _songContextMenu.RequiredHeight;

    if (yPos + menuHeight > Frame.Height) {
      // Not enough space below the row, so put it above
      // Let's position it so the bottom of the menu ends at p.Y
      yPos = yPos - menuHeight - 1;
    }

    _songContextMenu.MakeVisible(new Point(_songTable.Frame.X + tableAdornments.Left, yPos));
    _songContextMenu.SetFocus();
  }

  private async void OnLikedPlaylistCommand(string command, string args) {
    _mainWindow.SetMode(Mode.Playlist);

    var token = _loadPlaylistCtsResource.Replace(new CancellationTokenSource()).Token;

    try {
      ResetSongsTable(Messages.LOADING_SONGS);
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

  private void OnPlaylistCommand(string command, string args) {
    _mainWindow.SetMode(Mode.Playlist);
  }

  private void ResetSongsTable(string message = Messages.NO_SONGS) {
    _songsLabel.Visible = true;
    _songsLabel.Text = message;
    _songTable.Style.ShowHeaders = false;
    _songTable.ClearSongs();
  }
}