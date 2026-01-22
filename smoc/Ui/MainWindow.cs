using System.Reflection;
using Smoc.Services;
using Smoc.Streaming;
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
  private readonly NowPlaying _nowPlaying;
  private readonly IPlayerService _playerService;
  private readonly CommandService _commandService;
  private readonly IStreamingClient _streamingClient;
  private readonly HttpClient _httpClient;

  private Mode? _currentMode;
  private View? _preCommandFocusedView;
  private Mode? _preCommandMode;

  /// <summary>
  /// Initializes a new instance of the <see cref="MainWindow"/> class.
  /// </summary>
  /// <param name="streamingClient">The initialized streaming client.</param>
  public MainWindow(IStreamingClient streamingClient) {
    Width = Dim.Fill();
    Height = Dim.Fill();
    CanFocus = true;

    this._streamingClient = streamingClient;
    _playerService = new SoundFlowPlayerService(this, streamingClient);
    _commandService = new CommandService();
    _httpClient = new HttpClient();
    _nowPlaying = new NowPlaying(this, _playerService, _commandService, _httpClient);
    _commandLine = new CommandLine() {
      Y = Pos.AnchorEnd()
    };
    _statusBar = new StatusBar(_playerService) {
      Y = Pos.Top(_commandLine) - 1
    };
    _mainContent = new MainContent(this, _commandService, _playerService, streamingClient) {
      Y = Pos.Bottom(_nowPlaying),
      Height = Dim.Fill() - _statusBar.Height - _commandLine.Height
    };
    Add(_nowPlaying, _mainContent, _statusBar, _commandLine);

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
      var command = (e.Context as CommandContext<string>?)?.Binding;
      SetMode(_preCommandMode!.Value);
      _preCommandFocusedView?.SetFocus();

      if (command is not null && command.Length > 0) {
        if (!_commandService.ExecuteCommand(command!)) {
          _commandLine.DisplayError($"not a valid commmand: {command}");
        }
      }
    };

    _currentMode = null;
    SetMode(Mode.Player);
    _mainContent.SetFocus();
  }

  /// <inheritdoc/>
  public void SetMode(Mode mode) {
    if (mode == _currentMode) {
      return;
    }

    if (mode != Mode.Command) {
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
    base.Dispose(disposing);
    _httpClient.Dispose();
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
