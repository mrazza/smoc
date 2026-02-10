using System.Reflection;
using Smoc.Configuration;
using Smoc.Services;
using Smoc.Services.Audio.SoundFlow;
using Smoc.Services.Streaming;
using Smoc.Streaming;
using Smoc.Ui.Drawing;
using Smoc.Ui.Models;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Smoc.Ui;

/// <summary>
/// The main application window management class.
/// </summary>
public sealed class MainWindow : Runnable, IMainWindow {
  private readonly CommandLine _commandLine;
  private readonly StatusBar _statusBar;
  private readonly MainContent _mainContent;
  private readonly NowPlayingBar _nowPlayingBar;
  private readonly IPlaybackQueueService _playbackQueueService;
  private readonly CommandService _commandService;
  private readonly HttpClient _httpClient;
  private readonly IPlaybackTrackingService _playbackTrackingService;
  private readonly ISixelDriver _sixelDriver;

  private Mode? _currentMode;
  private View? _preCommandFocusedView;
  private Mode? _preCommandMode;

  /// <inheritdoc/>
  public ISixelDriver SixelDriver => _sixelDriver;

  /// <summary>
  /// Initializes a new instance of the <see cref="MainWindow"/> class.
  /// </summary>
  /// <param name="streamingClient">The initialized streaming client.</param>
  public MainWindow(IStreamingClient streamingClient, ISixelDriver sixelDriver) {
    Width = Dim.Fill();
    Height = Dim.Fill();
    CanFocus = true;

    _sixelDriver = sixelDriver;
    _playbackQueueService = StandardPlaybackQueueService.UsingAudioService<SoundFlowAudioService>(this, streamingClient);
    _playbackTrackingService = new StreamingListenHistoryService(
      streamingClient,
      TimeSpan.FromSeconds(ListenHistoryConfig.MinimumPositionSeconds),
      ListenHistoryConfig.MinimumFraction);
    if (ListenHistoryConfig.Enabled) {
      _playbackQueueService.PositionChanged += (_, position) => {
        if (_playbackQueueService.CurrentSong is { } song) {
          _playbackTrackingService.TrackPlayback(song, position);
        }
      };
    }

    _commandService = new CommandService();
    _httpClient = new HttpClient();
    _nowPlayingBar = new NowPlayingBar(this, _playbackQueueService, _commandService, _httpClient);
    _commandLine = new CommandLine() {
      Y = Pos.AnchorEnd()
    };
    _statusBar = new StatusBar(_playbackQueueService) {
      Y = Pos.Top(_commandLine) - 1
    };
    _mainContent = new MainContent(this, _commandService, _playbackQueueService, streamingClient, _httpClient) {
      Y = Pos.Bottom(_nowPlayingBar),
      Height = Dim.Fill() - _statusBar.Height - _commandLine.Height
    };
    Add(_nowPlayingBar, _mainContent, _statusBar, _commandLine);

    _commandService.RegisterCommand("q", (_, args) => {
      if (args.Length > 0) {
        _commandLine.DisplayError($"unexpected trailing characters: {args}");
      } else {
        App!.RequestStop();
      }
    });

    AddCommand(Command.HotKey, OnCommandLineHotKey);
    HotKeyBindings.Add(new Key(':'), this, Command.HotKey);

    _commandLine.CommandCancelled += (sender, e) => {
      SetMode(_preCommandMode!.Value);
      _preCommandFocusedView?.SetFocus();
    };
    _commandLine.Accepted += (sender, e) => {
      var command = (e.Context as CommandLineCommandContext?)?.CommandText;
      SetMode(_preCommandMode!.Value);
      _preCommandFocusedView?.SetFocus();

      if (command is not null && command.Length > 0) {
        if (!_commandService.ExecuteCommand(command)) {
          _commandLine.DisplayError($"not a valid commmand: {command}");
        }
      }
    };

    _currentMode = null;
    SetMode(Mode.Queue);
    _mainContent.SetFocus();
  }

  /// <inheritdoc/>
  public void SetMode(Mode mode) {
    if (mode == _currentMode) {
      return;
    }

    if (mode != Mode.Command) {
      switch (mode) {
        case Mode.NowPlaying:
          _nowPlayingBar.Visible = false;
          break;
        default:
          _nowPlayingBar.Visible = true;
          break;
      }
      _mainContent.SetMode(mode);
    } else {
      _commandLine.SetFocus();
    }

    _currentMode = mode;
    _statusBar.SetMode(GetModeDisplayName(mode));
  }

  /// <inheritdoc/>
  public void DisplayError(string message) {
    _commandLine.DisplayError(message);
  }

  protected override void Dispose(bool disposing) {
    _httpClient.Dispose();
    _commandService.UnregisterCommand("q");
    base.Dispose(disposing);
  }

  private bool? OnCommandLineHotKey(ICommandContext? context) {
    _preCommandMode = _currentMode;
    _preCommandFocusedView = MostFocused;
    SetMode(Mode.Command);
    return true;
  }

  private static string GetModeDisplayName(Mode mode) {
    FieldInfo? fieldInfo = typeof(Mode).GetField(mode.ToString());
    if (fieldInfo is not null) {
      object[] attributes = fieldInfo.GetCustomAttributes(typeof(DisplayNameAttribute), true);
      if (attributes.Length > 0) {
        return ((DisplayNameAttribute)attributes[0]).DisplayName;
      }
    }
    throw new ArgumentException("Invalid mode");
  }
}
