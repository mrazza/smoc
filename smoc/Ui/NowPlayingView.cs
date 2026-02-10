namespace Smoc.Ui;

using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui.Components;
using Smoc.Ui.Models;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

/// <summary>
/// A view that displays the current playback queue in a table.
/// </summary>
public sealed class NowPlayingView : View {

  private static class Messages {
    public const string NO_SONG = "no track";
    public const string NO_ARTIST = "no  artist";
  }

  private const float _albumArtMaxViewportPercent = 0.5f;

  private readonly IPlaybackQueueService _playbackQueueService;
  private readonly HttpClient _httpClient;
  private readonly IMainWindow _mainWindow;
  private readonly CommandService _commandService;
  private readonly SixelImageView _albumArtView;
  private readonly Label _songLabel;
  private readonly Label _artistLabel;
  private readonly View _progressContainer;
  private readonly Label _positionLabel;
  private readonly ProgressBar _progressBar;
  private readonly Label _durationLabel;
  private string? _albumArtUrl;
  private CancellationTokenSource? _albumArtCancellationTokenSource;


  /// <summary>
  /// Initializes a new instance of the <see cref="NowPlayingView"/> class.
  /// </summary>
  /// <param name="mainWindow">The main window reference.</param>
  /// <param name="commandService">The command service.</param>
  /// <param name="playerService">The player service for managing the queue.</param>
  public NowPlayingView(IMainWindow mainWindow, CommandService commandService, IPlaybackQueueService playbackQueueService, HttpClient httpClient) {
    _mainWindow = mainWindow;
    _commandService = commandService;
    _playbackQueueService = playbackQueueService;
    _httpClient = httpClient;
    Width = Dim.Fill();
    Height = Dim.Fill();
    _albumArtView = new SixelImageView(_mainWindow) {
      X = Pos.Center(),
      Y = Pos.Center() - Pos.Percent(10),
      Height = Dim.Func((view) => {
        float maxHeight = view!.Frame.Height * _albumArtMaxViewportPercent;
        float maxWidth = view!.Frame.Width * _albumArtMaxViewportPercent;
        return (int)Math.Round(Math.Min(maxHeight, maxWidth / _mainWindow.SixelDriver.CellAspectRatio));
      }, this),
      Width = Dim.Func((view) => {
        float maxHeight = view!.Frame.Height * _albumArtMaxViewportPercent;
        float maxWidth = view!.Frame.Width * _albumArtMaxViewportPercent;
        double height = Math.Min(maxHeight, maxWidth / _mainWindow.SixelDriver.CellAspectRatio);
        return (int)Math.Round(height * _mainWindow.SixelDriver.CellAspectRatio);
      }, this),
      BorderStyle = LineStyle.Dashed,
      TextAlignment = Alignment.Center,
      Text = "??"
    };
    _albumArtView.Margin!.Thickness = new Thickness(0, 0, 1, 1);

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
      ProgressBarStyle = ProgressBarStyle.Continuous
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

    Add(_albumArtView, _songLabel, _artistLabel, _progressContainer);

    _playbackQueueService.SongChanged += OnSongChanged;
    _playbackQueueService.PositionChanged += OnPositionChanged;

    _commandService.RegisterCommand("np", OnNowPlayingCommand);
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
    if (song.Album.LargeThumbnailUrl is null || song.Album.LargeThumbnailUrl.Length == 0) {
      _albumArtView.ClearImage();
    } else if (_albumArtUrl != song.Album.LargeThumbnailUrl) {
      _albumArtUrl = song.Album.LargeThumbnailUrl;
      _albumArtCancellationTokenSource?.Cancel();
      _albumArtCancellationTokenSource = new CancellationTokenSource();
      var token = _albumArtCancellationTokenSource.Token;
      try {
        var albumResponse = await _httpClient.GetAsync(song.Album.LargeThumbnailUrl, token);
        var image = Image.Load<Rgba32>(albumResponse.Content.ReadAsStream());
        Logging.Debug($"Album art loaded: {song.Title}");
        token.ThrowIfCancellationRequested();
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
    _playbackQueueService.SongChanged -= OnSongChanged;
    _playbackQueueService.PositionChanged -= OnPositionChanged;
    base.Dispose(disposing);
  }

  private void OnNowPlayingCommand(string _, string __) {
    _mainWindow.SetMode(Mode.NowPlaying);
  }

  private void Reset() {
    _songLabel.Text = Messages.NO_SONG;
    _artistLabel.Text = Messages.NO_ARTIST;
    _positionLabel.Text = "--:--";
    _durationLabel.Text = "--:--";
    _albumArtView.ClearImage();
    _progressBar.Fraction = 0.0f;
  }
}
