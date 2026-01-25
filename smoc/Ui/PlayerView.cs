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
  private readonly IPlaybackQueueService _playbackQueueService;
  private readonly IMainWindow _mainWindow;

  /// <summary>
  /// Initializes a new instance of the <see cref="PlayerView"/> class.
  /// </summary>
  /// <param name="mainWindow">The main window reference.</param>
  /// <param name="commandService">The command service.</param>
  /// <param name="playerService">The player service for managing the queue.</param>
  public PlayerView(IMainWindow mainWindow, CommandService commandService, IPlaybackQueueService playbackQueueService) {
    _mainWindow = mainWindow;
    _playbackQueueService = playbackQueueService;
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

    _playbackQueueService.QueueChanged += OnQueueChanged;
    _playbackQueueService.SongChanged += OnSongChanged;

    _songTable.SongSelected += OnSongSelected;
  }

  protected override void Dispose(bool disposing) {
    _playbackQueueService.QueueChanged -= OnQueueChanged;
    _playbackQueueService.SongChanged -= OnSongChanged;
    _songTable.SongSelected -= OnSongSelected;
    base.Dispose(disposing);
  }

  protected override void OnVisibleChanged() {
    base.OnVisibleChanged();

    if (Visible && _playbackQueueService.CurrentSong is { }) {
      _songTable.SelectedRow = _playbackQueueService.CurrentPlaybackIndex;
      _songTable.EnsureSelectedCellIsVisible();
    }
  }

  private async void OnSongSelected(object? sender, List<Song> songs) {
    if (songs.Count > 1) {
      throw new InvalidOperationException("Multiple songs cannot be selected when changing active track");
    }

    await _playbackQueueService.ChangeTrack(_songTable.SelectedRow);
  }

  private void OnSongChanged(object? sender, Song? song) {
    _songTable.HighlightedRow = _playbackQueueService.CurrentPlaybackIndex;
  }

  private void OnQueueChanged(object? sender, EventArgs e) {
    var queue = _playbackQueueService.GetCurrentPlaybackQueue();
    if (!queue.Any()) {
      _noSongsLabel.Visible = true;
      _songTable.Style.ShowHeaders = false;
      _songTable.ClearSongs();
    } else {
      _noSongsLabel.Visible = false;
      _songTable.Style.ShowHeaders = true;
      _songTable.SetSongs(queue);
      _songTable.SelectedRow = _playbackQueueService.CurrentPlaybackIndex;
    }
  }

  private void OnNowPlayingCommand(string command, string args) {
    _mainWindow.SetMode(Mode.Player);
  }
}
