using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui.Models;
using Terminal.Gui.ViewBase;

namespace Smoc.Ui;

public sealed class MainContent : View {
  private readonly ArtistView _artistView;
  private readonly PlayerView _playerView;
  private readonly SongView _songView;
  private readonly MainWindow _mainWindow;

  private View? _currentView;

  public MainContent(MainWindow mainWindow, CommandService commandService, PlayerService playerService, IStreamingClient streamingClient) {
    _mainWindow = mainWindow;
    Width = Dim.Fill();
    Height = Dim.Fill();
    CanFocus = true;
    _currentView = null;

    _artistView = new ArtistView(mainWindow, commandService, streamingClient, playerService) {
      Visible = false
    };
    _playerView = new PlayerView(mainWindow, commandService, playerService) {
      Visible = false
    };
    _songView = new SongView(mainWindow, commandService, streamingClient, playerService) {
      Visible = false
    };
    Add(_artistView, _playerView, _songView);
  }

  public void SetMode(Mode mode) {
    if (_currentView is not null) {
      _currentView.Visible = false;
    }

    _currentView = mode switch {
      Mode.Player => _playerView,
      Mode.Artist => _artistView,
      Mode.Song => _songView,
      _ => throw new ArgumentException("Invalid mode"),
    };
    _currentView!.Visible = true;
  }
}
