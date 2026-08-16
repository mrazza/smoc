namespace Smoc.Ui;

using System;
using Smoc.Services;
using Smoc.Configuration;
using Smoc.Streaming;
using Smoc.Ui.Components;
using Smoc.Ui.Models;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

/// <summary>
/// A view that displays the currently playing track with album art and progress information.
/// </summary>
public sealed class NowPlayingView : View {

  private static class Messages {
    public const string NO_SONG = "no track";
    public const string NO_ARTIST = "no  artist";
  }

  private const float _albumArtMaxViewportPercent = 0.5f;

  private readonly IPlaybackQueueService _playbackQueueService;
  private readonly IStreamingClient _streamingClient;
  private readonly IMainWindow _mainWindow;
  private readonly CommandService _commandService;
  private readonly SixelImageView _albumArtView;
  private readonly FrequencyHistogramView _histogramView;
  private readonly Label _songLabel;
  private readonly Label _artistLabel;
  private readonly View _progressContainer;
  private readonly Label _positionLabel;
  private readonly ProgressBar _progressBar;
  private readonly Label _durationLabel;
  private Album? _currentAlbum;
  private CancellationTokenSource? _albumArtCancellationTokenSource;
  private bool _showVisualization = false;


  /// <summary>
  /// Initializes a new instance of the <see cref="NowPlayingView"/> class.
  /// </summary>
  /// <param name="mainWindow">The main window reference.</param>
  /// <param name="commandService">The command service.</param>
  /// <param name="playbackQueueService">The playback queue service for managing the queue.</param>
  /// <param name="streamingClient">The streaming client for downloading album art.</param>
  public NowPlayingView(IMainWindow mainWindow, CommandService commandService, IPlaybackQueueService playbackQueueService, IStreamingClient streamingClient) {
    _mainWindow = mainWindow;
    _commandService = commandService;
    _playbackQueueService = playbackQueueService;
    _streamingClient = streamingClient;
    Width = Dim.Fill();
    Height = Dim.Fill();
    _albumArtView = new SixelImageView() {
      X = Pos.Center(),
      Y = Pos.Center() - Pos.Percent(10),
      SixelEncoder = new SixelEncoder(),
      Height = Dim.Func((view) => {
        float maxHeight = view!.Frame.Height * _albumArtMaxViewportPercent;
        float maxWidth = view!.Frame.Width * _albumArtMaxViewportPercent;
        var resolution = App?.Driver?.SixelSupport?.Resolution ?? new System.Drawing.Size(1, 2);
        return (int)Math.Round(Math.Min(maxHeight, maxWidth / ((double)resolution.Height / resolution.Width)));
      }, this),
      Width = Dim.Func((view) => {
        float maxHeight = view!.Frame.Height * _albumArtMaxViewportPercent;
        float maxWidth = view!.Frame.Width * _albumArtMaxViewportPercent;
        var resolution = App?.Driver?.SixelSupport?.Resolution ?? new System.Drawing.Size(1, 2);
        double height = Math.Min(maxHeight, maxWidth / ((double)resolution.Height / resolution.Width));
        return (int)Math.Round(height * ((double)resolution.Height / resolution.Width));
      }, this),
      BorderStyle = LineStyle.Dashed,
      TextAlignment = Alignment.Center,
      VerticalTextAlignment = Alignment.Center,
      Text = "??"
    };
    _albumArtView.Margin!.Thickness = new Thickness(0, 0, 1, 1);

    _histogramView = new FrequencyHistogramView(playbackQueueService) {
      X = Pos.Center(),
      Y = Pos.Center() - Pos.Percent(10),
      Width = _albumArtView.Width, 
      Height = _albumArtView.Height,
      Visible = false
    };
    _histogramView.Margin!.Thickness = new Thickness(0, 0, 0, 1);

    _songLabel = new Label() {
      X = Pos.Center(),
      Y = Pos.Bottom(_albumArtView),
      TextAlignment = Alignment.Center
    };
    _artistLabel = new Label() {
      X = Pos.Center(),
      Y = Pos.Bottom(_songLabel),
      TextAlignment = Alignment.Center
    };
    _artistLabel.Margin!.Thickness = new Thickness(0, 0, 0, 1);

    _progressContainer = new View() {
      X = Pos.Center(),
      Y = Pos.Bottom(_artistLabel),
      Width = Dim.Func((artView) => artView!.Frame.Width, _albumArtView),
      Height = Dim.Absolute(2)
    };
    _progressBar = new ProgressBar() {
      Width = Dim.Fill(),
      ProgressBarStyle = ProgressBarStyle.Continuous,
      SchemeName = "ProgressBar"
    };
    _positionLabel = new Label() {
      X = Pos.Absolute(0),
      Y = Pos.Bottom(_progressBar)
    };

    _durationLabel = new Label() {
      X = Pos.AnchorEnd(),
      Y = Pos.Bottom(_progressBar)
    };
    _progressBar.Fraction = 0.5f;
    _progressContainer.Add(_positionLabel, _durationLabel, _progressBar);

    Reset();

    Add(_albumArtView, _histogramView, _songLabel, _artistLabel, _progressContainer);

    _playbackQueueService.SongChanged += OnSongChanged;
    _playbackQueueService.PositionChanged += OnPositionChanged;

    _commandService.RegisterCommand("np", OnNowPlayingCommand);
    _commandService.RegisterCommand("np-vis", OnToggleVisualizationCommand);
    _commandService.RegisterCommand("np-vis-fps", OnSetFpsCommand);

    AddCommand(Command.HotKey, OnHotKey);
    HotKeyBindings.Add(Key.V, Command.HotKey);
    HotKeyBindings.Add(Key.V.WithShift, Command.HotKey);
  }

  private void OnPositionChanged(object? sender, TimeSpan e) {
    _positionLabel.Text = e.ToString("mm\\:ss");
    _durationLabel.Text = _playbackQueueService.Duration.ToString("mm\\:ss");

    var progress = (float)Math.Round(e / _playbackQueueService.Duration, 2);
    if (_progressBar.Fraction != progress) {
      _progressBar.Fraction = progress;
    }
  }

  private async void OnSongChanged(object? sender, Song? song) {
    if (song is not { }) {
      Reset();
      return;
    }

    _songLabel.Text = song.Title;
    _artistLabel.Text = song.Artist.Name;

    // Only bother downloading the album art if it has changed.
    if (!song.Album.Covers.Any()) {
      _albumArtView.ClearImage();
      _albumArtView.BorderStyle = LineStyle.Dashed;
    } else if (_currentAlbum != song.Album) {
      _currentAlbum = song.Album;
      _albumArtCancellationTokenSource?.Cancel();
      _albumArtCancellationTokenSource = new CancellationTokenSource();
      var token = _albumArtCancellationTokenSource.Token;
      try {
        var image = await _streamingClient.GetAlbumArtAsync(song.Album, (covers) => covers.OrderByDescending(c => c.Width).First(), token);
        Logging.Debug($"Album art loaded: {song.Title}");
        token.ThrowIfCancellationRequested();
        _albumArtView.BorderStyle = LineStyle.None;
        _albumArtView.SetImage(image);
      } catch (OperationCanceledException) {
        Logging.Debug($"Album art load cancelled: {song.Title}");
      } catch (Exception ex) {
        Logging.Error($"Failed to load album art for {song.Title}: {ex.Message}");
      }
    }
  }

  protected override void Dispose(bool disposing) {
    _commandService.UnregisterCommand("np");
    _commandService.UnregisterCommand("np-vis");
    _commandService.UnregisterCommand("np-vis-fps");
    _playbackQueueService.SongChanged -= OnSongChanged;
    _playbackQueueService.PositionChanged -= OnPositionChanged;
    base.Dispose(disposing);
  }

  private void OnNowPlayingCommand(string _, string __) {
    _mainWindow.SetMode(Mode.NowPlaying);
  }

  private void Reset() {
    _showVisualization = false;
    _albumArtView.Visible = true;
    _histogramView.Visible = false;
    _playbackQueueService.IsSpectrumActive = false;
    _songLabel.Text = Messages.NO_SONG;
    _artistLabel.Text = Messages.NO_ARTIST;
    _positionLabel.Text = "--:--";
    _durationLabel.Text = "--:--";
    _albumArtView.ClearImage();
    _albumArtView.BorderStyle = LineStyle.Dashed;
    _progressBar.Fraction = 0.0f;
  }

  private void ToggleVisualization() {
    _showVisualization = !_showVisualization;
    _albumArtView.Visible = !_showVisualization;
    _histogramView.Visible = _showVisualization;
    _playbackQueueService.IsSpectrumActive = _showVisualization;
    SetNeedsDraw();
  }

  private void OnToggleVisualizationCommand(string _, string __) {
    ToggleVisualization();
  }

  private void OnSetFpsCommand(string command, string args) {
    var splitArgs = CommandService.GetArgs(args);
    if (splitArgs.Length == 0) {
      return;
    }

    if (!int.TryParse(splitArgs[0], out int fps) || fps < 1 || fps > 60) {
      Logging.Warning($"Invalid FPS: {splitArgs[0]}");
      _mainWindow.DisplayError($"invalid FPS: {splitArgs[0]} ([1-60] expected)");
      return;
    }

    SmocConfiguration.Defaults.VisualizerFps = fps;
  }

  private bool? OnHotKey(ICommandContext? ctx) {
    if (ctx?.Binding is KeyBinding keyBinding && keyBinding.Key is Key pressedKey) {
      if (pressedKey == Key.V || pressedKey == Key.V.WithShift) {
        ToggleVisualization();
        return true;
      }
    }
    return null;
  }
}
