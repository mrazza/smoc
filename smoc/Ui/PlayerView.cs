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

/// <summary>
/// A view that displays the current playback queue in a table.
/// </summary>
public sealed class PlayerView : View {
  private readonly SongTable _songTable;
  private readonly Label _noSongsLabel;
  private readonly IPlayerService _playerService;
  private readonly MainWindow _mainWindow;

  /// <summary>
  /// Initializes a new instance of the <see cref="PlayerView"/> class.
  /// </summary>
  /// <param name="mainWindow">The main window reference.</param>
  /// <param name="commandService">The command service.</param>
  /// <param name="playerService">The player service for managing the queue.</param>
  public PlayerView(MainWindow mainWindow, CommandService commandService, IPlayerService playerService) {
    _mainWindow = mainWindow;
    _playerService = playerService;
    Width = Dim.Fill();
    Height = Dim.Fill();
    CanFocus = true;
    _noSongsLabel = new Label() {
      X = Pos.Center(),
      Y = Pos.Center(),
      Text = "queue is empty"
    };
    _songTable = new SongTable(SongTableColumns.Artist | SongTableColumns.Album | SongTableColumns.Song | SongTableColumns.Length) {
      Width = Dim.Fill(),
      Height = Dim.Fill(),
      MultiSelect = false // TODO: allow multiple selection for remove/reorder
    };
    _songTable.Style.ShowHeaders = false;
    _songTable.BorderStyle = LineStyle.Single;

    Add(_songTable, _noSongsLabel);

    commandService.RegisterCommand("np", OnNowPlayingCommand);

    playerService.QueueChanged += OnQueueChanged;
    playerService.SongChanged += OnSongChanged;

    _songTable.SongSelected += OnSongSelected;
  }

  protected override void Dispose(bool disposing) {
    _playerService.QueueChanged -= OnQueueChanged;
    _playerService.SongChanged -= OnSongChanged;
    _songTable.SongSelected -= OnSongSelected;
    base.Dispose(disposing);
  }

  protected override void OnVisibleChanged() {
    base.OnVisibleChanged();

    if (Visible && _playerService.CurrentSong is Song currentSong) {
      _songTable.SelectedRow = _playerService.CurrentPlaybackIndex;
      _songTable.EnsureSelectedCellIsVisible();
    }
  }

  private async void OnSongSelected(object? sender, List<Song> songs) {
    if (songs.Count > 1) {
      throw new InvalidOperationException("Multiple songs cannot be selected when changing active track");
    }

    await _playerService.ChangeTrack(_songTable.SelectedRow);
  }

  private void OnSongChanged(object? sender, Song e) {
    _songTable.HighlightedRow = _playerService.CurrentPlaybackIndex;
  }

  private void OnQueueChanged(object? sender, EventArgs e) {
    var queue = _playerService.GetCurrentPlaybackQueue();
    if (!queue.Any()) {
      _noSongsLabel.Visible = true;
      _songTable.Style.ShowHeaders = false;
    }
    else {
      _noSongsLabel.Visible = false;
      _songTable.Style.ShowHeaders = true;
      _songTable.SetSongs(queue);
      _songTable.SelectedRow = _playerService.CurrentPlaybackIndex;
    }
  }

  private void OnNowPlayingCommand(string command, string args) {
    _mainWindow.SetMode(Mode.Player);
  }
}
