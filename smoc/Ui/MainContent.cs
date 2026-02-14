using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui.Models;
using Terminal.Gui.ViewBase;

namespace Smoc.Ui;

/// <summary>
/// The main content area of the application, manages switching between different views based on the current mode.
/// </summary>
public sealed class MainContent : View {
  private readonly ArtistView _artistView;
  private readonly PlaybackQueueView _playbackQueueView;
  private readonly SongView _songView;
  private readonly PlaylistView _playlistView;
  private readonly NowPlayingView _nowPlayingView;

  private View? _currentView;

  /// <summary>
  /// Initializes a new instance of the <see cref="MainContent"/> class.
  /// </summary>
  /// <param name="mainWindow">The main window reference.</param>
  /// <param name="commandService">The command service.</param>
  /// <param name="playbackQueueService">The playback queue service.</param>
  /// <param name="streamingClient">The streaming client.</param>
  public MainContent(IMainWindow mainWindow, CommandService commandService, IPlaybackQueueService playbackQueueService, IStreamingClient streamingClient) {
    Width = Dim.Fill();
    Height = Dim.Fill();
    CanFocus = true;
    _currentView = null;

    _artistView = new ArtistView(mainWindow, commandService, streamingClient, playbackQueueService) {
      Visible = false
    };
    _playbackQueueView = new PlaybackQueueView(mainWindow, commandService, playbackQueueService) {
      Visible = false
    };
    _songView = new SongView(mainWindow, commandService, streamingClient, playbackQueueService) {
      Visible = false
    };
    _playlistView = new PlaylistView(mainWindow, commandService, playbackQueueService, streamingClient) {
      Visible = false
    };
    _nowPlayingView = new NowPlayingView(mainWindow, commandService, playbackQueueService, streamingClient) {
      Visible = false
    };
    Add(_artistView, _playbackQueueView, _songView, _playlistView, _nowPlayingView);
  }

  /// <summary>
  /// Switches the visible view to match the specified mode.
  /// </summary>
  /// <param name="mode">The mode to switch to.</param>
  /// <exception cref="ArgumentException">Thrown if the mode is invalid.</exception>
  public void SetMode(Mode mode) {
    if (_currentView is not null) {
      _currentView.Visible = false;
    }

    _currentView = mode switch {
      Mode.Queue => _playbackQueueView,
      Mode.Artist => _artistView,
      Mode.Song => _songView,
      Mode.Playlist => _playlistView,
      Mode.NowPlaying => _nowPlayingView,
      _ => throw new ArgumentException("Invalid mode"),
    };
    _currentView!.Visible = true;
  }
}
