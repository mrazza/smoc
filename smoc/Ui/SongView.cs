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

/// <summary>
/// A view for searching songs directly.
/// </summary>
public sealed class SongView : View {
  private static class Messages {
    public const string SEARCHING = "searching...";
    public const string NO_SONGS = "no tracks found";
    public const string SEARCH_ERROR = "error searching tracks";
  }

  private readonly MainWindow _mainWindow;
  private readonly SongTable _songTable;
  private readonly SongContextMenu _songContextMenu;
  private readonly Label _songsLabel;
  private readonly IStreamingClient _streamingClient;
  private readonly PlayerService _playerService;

  private CancellationTokenSource? _searchCts;

  /// <summary>
  /// Initializes a new instance of the <see cref="SongView"/> class.
  /// </summary>
  /// <param name="mainWindow">The main window reference.</param>
  /// <param name="commandService">The command service for registering search commands.</param>
  /// <param name="streamingClient">The client for searching songs.</param>
  /// <param name="playerService">The player service for playback options.</param>
  public SongView(MainWindow mainWindow, CommandService commandService, IStreamingClient streamingClient, PlayerService playerService) {
    _mainWindow = mainWindow;
    _streamingClient = streamingClient;
    _playerService = playerService;

    Width = Dim.Fill();
    Height = Dim.Fill();
    CanFocus = true;

    _songsLabel = new Label() {
      X = Pos.Center(),
      Y = Pos.Center(),
      Text = Messages.SEARCHING
    };
    _songTable = new SongTable(SongTableColumns.Artist | SongTableColumns.Album | SongTableColumns.Song | SongTableColumns.Length) {
      Width = Dim.Fill(),
      Height = Dim.Fill()
    };
    _songTable.Style.ShowHeaders = false;
    _songTable.BorderStyle = LineStyle.Single;
    _songTable.SongSelected += OnSongSelected;
    _songContextMenu = new SongContextMenu(playerService, _songTable);

    Add(_songTable, _songsLabel, _songContextMenu);

    commandService.RegisterCommand("t", OnTrackSearchCommand);
  }

  protected override void Dispose(bool disposing) {
    CancelPendingSearches();
    _songTable.SongSelected -= OnSongSelected;
    base.Dispose(disposing);
  }

  private void OnSongSelected(object? sender, List<Song> songs) {
    var tableAdornments = _songTable.GetAdornmentsThickness();
    var yPos = _songTable.SelectedRow + tableAdornments.Top + 2;
    int menuHeight = _songContextMenu.RequiredHeight;

    if (yPos + menuHeight > Frame.Height) {
      // Not enough space below the row, so put it above
      // Let's position it so the bottom of the menu ends at p.Y
      yPos = yPos - menuHeight - 1;
    }

    _songContextMenu.MakeVisible(new Point(_songTable.Frame.X + tableAdornments.Left, yPos));
    _songContextMenu.SetFocus();
  }

  private async void OnTrackSearchCommand(string command, string args) {
    _mainWindow.SetMode(Mode.Song);

    if (args.Length == 0) {
      return;
    }

    if (args[0] == '/') {
      args = args[1..];
    }

    CancelPendingSearches();
    _searchCts = new CancellationTokenSource();
    var token = _searchCts.Token;

    ResetTable();

    try {
      Logging.Information($"Searching for track {args}...");
      var songs = await _streamingClient.SearchSongsAsync(args, token);

      if (token.IsCancellationRequested) return;

      Logging.Information($"Found {songs.Count} tracks for search '{args}'.");

      if (songs.Count == 0) {
        ResetTable(Messages.NO_SONGS);
        return;
      }

      _songTable.Style.ShowHeaders = true;
      _songsLabel.Visible = false;
      _songTable.SetSongs(songs);
    }
    catch (OperationCanceledException) {
      // Ignore
    }
    catch (Exception ex) {
      Logging.Error($"Error searching tracks: {ex.Message}");
      _mainWindow.DisplayError(Messages.SEARCH_ERROR);
      ResetTable(Messages.SEARCH_ERROR);
    }
  }

  private void CancelPendingSearches() {
    _searchCts?.Cancel();
    _searchCts?.Dispose();
    _searchCts = null;
  }

  private void ResetTable(string message = Messages.SEARCHING) {
    _songsLabel.Visible = true;
    _songsLabel.Text = message;
    _songTable.Style.ShowHeaders = false;
    _songTable.ClearSongs();
  }
}
