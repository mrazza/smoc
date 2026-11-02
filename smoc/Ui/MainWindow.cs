using System.Reflection;
using Smoc.Configuration;
using Smoc.Services;
using Smoc.Services.Audio;
using Smoc.Services.Audio.SoundFlow;
using Smoc.Services.Streaming;
using Smoc.Streaming;
using Smoc.Ui.Models;
using Terminal.Gui.App;
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
  private readonly IAudioService _audioService;
  private readonly IPlaybackQueueService _playbackQueueService;
  private readonly CommandService _commandService;
  private readonly IPlaybackTrackingService _playbackTrackingService;
  private readonly ISessionStateService _sessionStateService;

  private Mode? _currentMode;
  private View? _preCommandFocusedView;
  private Mode? _preCommandMode;

  /// <summary>
  /// Initializes a new instance of the <see cref="MainWindow"/> class.
  /// </summary>
  /// <param name="streamingClient">The initialized streaming client.</param>
  /// <param name="sessionStateService">Optional session state service for persisting playback state across restarts.</param>
  /// <param name="audioService">Optional audio service for managing audio playback and sound devices.</param>
  public MainWindow(IStreamingClient streamingClient, ISessionStateService? sessionStateService = null, IAudioService? audioService = null) {
    Width = Dim.Fill();
    Height = Dim.Fill();
    CanFocus = true;

    _sessionStateService = sessionStateService ?? new JsonSessionStateService();
    _audioService = audioService ?? new SoundFlowAudioService();
    _playbackQueueService = new StandardPlaybackQueueService(this, streamingClient, _audioService);
    _playbackTrackingService = new StreamingListenHistoryService(
      streamingClient,
      TimeSpan.FromSeconds(ListenHistoryConfig.Defaults.MinimumPositionSeconds),
      ListenHistoryConfig.Defaults.MinimumFraction);
    if (ListenHistoryConfig.Defaults.Enabled) {
      _playbackQueueService.PositionChanged += (_, position) => {
        if (_playbackQueueService.CurrentSong is { } song) {
          _playbackTrackingService.TrackPlayback(song, position);
        }
      };
    }

    _commandService = new CommandService();
    _nowPlayingBar = new NowPlayingBar(this, _playbackQueueService, _commandService, streamingClient);
    _commandLine = new CommandLine(_commandService) {
      Y = Pos.AnchorEnd()
    };
    _statusBar = new StatusBar(_playbackQueueService) {
      Y = Pos.Top(_commandLine) - 1
    };
    _mainContent = new MainContent(this, _commandService, _playbackQueueService, streamingClient) {
      Y = Pos.Bottom(_nowPlayingBar),
      Height = Dim.Fill() - _statusBar.Height - _commandLine.Height
    };
    Add(_nowPlayingBar, _mainContent, _statusBar, _commandLine);

    _commandService.RegisterCommand("q", (_, args) => {
      if (args.Length > 0) {
        _commandLine.DisplayError($"unexpected trailing characters: {args}");
      } else {
        SaveSessionState();
        App!.RequestStop();
      }
    });
    _commandService.RegisterCommand("devices", OnDevicesCommand);
    _commandService.RegisterCommand("device", OnDeviceCommand);

    AddCommand(Command.HotKey, OnCommandLineHotKey);
    HotKeyBindings.Add(new Key(':'), Command.HotKey);
    HotKeyBindings.Add(new Key(':').WithShift, Command.HotKey);
    HotKeyBindings.Add(new Key('/'), Command.HotKey);

    _commandLine.FilterChanged += (_, filterText) => {
      _mainContent.ApplyFilterToCurrentView(filterText);
    };
    _commandLine.FilterAccepted += (_, filterText) => {
      _mainContent.ApplyFilterToCurrentView(filterText);
    };
    _commandLine.CommandCancelled += (sender, e) => {
      if (_commandLine.InputMode == CommandLineMode.Filter) {
        _mainContent.ApplyFilterToCurrentView(string.Empty);
      }
      SetMode(_preCommandMode!.Value);
      _preCommandFocusedView?.SetFocus();
    };
    _commandLine.Accepted += (sender, e) => {
      var command = (e.Context as CommandLineCommandContext?)?.CommandText;
      bool isFilter = _commandLine.InputMode == CommandLineMode.Filter || (command is not null && command.StartsWith('/'));
      SetMode(_preCommandMode!.Value);
      _preCommandFocusedView?.SetFocus();

      if (!isFilter && command is not null && command.Length > 0) {
        if (!_commandService.ExecuteCommand(command)) {
          _commandLine.DisplayError($"not a valid command: {command}");
        }
      }
    };

    _currentMode = null;
    SetMode(Mode.Queue);
    _mainContent.SetFocus();

    if (_sessionStateService.Load() is { } savedSession) {
      _playbackQueueService.RestoreSessionState(savedSession);
    }
  }

  /// <inheritdoc/>
  public void SetMode(Mode mode) {
    if (mode == _currentMode) {
      return;
    }

    if (mode != Mode.Command && mode != Mode.Filter) {
      _nowPlayingBar.Visible = mode switch {
        Mode.NowPlaying => false,
        _ => true,
      };
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
    if (disposing) {
      SaveSessionState();
      _commandService.UnregisterCommand("q");
      _commandService.UnregisterCommand("devices");
      _commandService.UnregisterCommand("device");
      _playbackQueueService.Dispose();
    }
    base.Dispose(disposing);
  }

  private void SaveSessionState() {
    try {
      _sessionStateService.Save(_playbackQueueService.GetSessionState());
    } catch (Exception ex) {
      Logging.Warning($"Failed to save session state during shutdown: {ex.Message}");
    }
  }

  private void OnDevicesCommand(string _, string args) {
    if (!string.IsNullOrWhiteSpace(args)) {
      OnDeviceCommand("device", args);
      return;
    }

    var devices = _audioService.GetPlaybackDevices();
    if (devices.Count == 0) {
      _commandLine.DisplayError("no audio playback devices found");
      return;
    }

    var summary = string.Join(" | ", devices.Select(d => $"{(d.IsActive ? "*" : "")}[{d.Index}] {d.Name}"));
    _commandLine.DisplayError(summary);
  }

  private void OnDeviceCommand(string _, string args) {
    var splitArgs = CommandService.GetArgs(args);
    if (splitArgs.Length == 0) {
      OnDevicesCommand("devices", string.Empty);
      return;
    }

    string query = splitArgs[0];
    var devices = _audioService.GetPlaybackDevices();
    AudioDeviceInfo? matchedDevice = null;
    if (int.TryParse(query, out int index)) {
      matchedDevice = devices.FirstOrDefault(d => d.Index == index);
    }
    matchedDevice ??= devices.FirstOrDefault(d => d.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
                     ?? devices.FirstOrDefault(d => d.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

    if (matchedDevice is null || !_audioService.SelectPlaybackDevice(matchedDevice)) {
      _commandLine.DisplayError($"audio device not found: {query}");
    }
  }

  private bool? OnCommandLineHotKey(ICommandContext? context) {
    if (context?.Binding is not KeyBinding { Key: { } pressedKey }) {
      return false;
    }
    var (mode, inputMode) = pressedKey switch {
      _ when pressedKey == new Key(':') || pressedKey == new Key(':').WithShift => (Mode.Command, CommandLineMode.Command),
      _ when pressedKey == new Key('/') => (Mode.Filter, CommandLineMode.Filter),
      _ => throw new ArgumentOutOfRangeException(nameof(context), $"Unsupported hotkey: {pressedKey}"),
    };
    _preCommandMode = _currentMode;
    _preCommandFocusedView = MostFocused;
    SetMode(mode);
    _commandLine.StartInput(inputMode);
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
