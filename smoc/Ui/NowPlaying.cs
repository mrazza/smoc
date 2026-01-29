using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui.Components;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Smoc.Ui;

/// <summary>
/// A view that displays the currently playing track information, including album art, progress, and volume.
/// </summary>
public sealed class NowPlaying : View {
  private static class Messages {
    public const string NO_SONG = "no track";
    public const string NO_ARTIST = "no artist";
    public const string VOLUME = "volume: {0}%";
  }

  private static readonly Key _previousKey = new(',');
  private static readonly Key _nextKey = new('.');

  private readonly IMainWindow _mainWindow;
  private readonly IPlaybackQueueService _playbackQueueService;
  private string? _albumArtUrl;
  private readonly SixelImageView _albumArtView;
  private readonly Label _songLabel;
  private readonly Label _artistLabel;
  private readonly Label _positionLabel;
  private readonly ProgressBar _progressBar;
  private readonly Label _durationLabel;
  private readonly Label _volumeLabel;
  private readonly HttpClient _httpClient;
  private CancellationTokenSource? _albumArtCancellationTokenSource;

  /// <summary>
  /// Initializes a new instance of the <see cref="NowPlaying"/> class.
  /// </summary>
  /// <param name="mainWindow">The main window reference.</param>
  /// <param name="playbackQueueService">The player service for playback information.</param>
  /// <param name="commandService">The command service for registering volume commands.</param>
  public NowPlaying(IMainWindow mainWindow, IPlaybackQueueService playbackQueueService, CommandService commandService, HttpClient httpClient) {
    _mainWindow = mainWindow;
    _playbackQueueService = playbackQueueService;
    _httpClient = httpClient;
    _albumArtUrl = null;
    Width = Dim.Fill();
    Height = Dim.Absolute(3);
    Padding!.Thickness = new Thickness(0, 0, 1, 0);
    CanFocus = false;

    _albumArtView = new SixelImageView() {
      X = Pos.Absolute(1),
      Y = Pos.Absolute(0),
      Width = Dim.Absolute(7),
      Height = Dim.Fill(),
      BorderStyle = LineStyle.Dashed,
      TextAlignment = Alignment.Center,
      Text = "??"
    };
    _albumArtView.Margin!.Thickness = new Thickness(0, 0, 1, 0);

    _songLabel = new Label() {
      X = Pos.Right(_albumArtView),
      Y = Pos.Absolute(0)
    };

    _artistLabel = new Label() {
      X = Pos.Right(_albumArtView),
      Y = Pos.Absolute(1)
    };

    _positionLabel = new Label() {
      X = Pos.Right(_albumArtView),
      Y = Pos.Absolute(2)
    };

    _durationLabel = new Label() {
      X = Pos.AnchorEnd(),
      Y = Pos.Absolute(2)
    };

    _progressBar = new ProgressBar() {
      X = Pos.Right(_positionLabel),
      Y = Pos.Absolute(2),
      Width = Dim.Fill() - Dim.Func((view) => view!.Frame.Width, _durationLabel),
      ProgressBarStyle = ProgressBarStyle.Continuous
    };

    _progressBar.Fraction = 0.5f;
    _progressBar.Margin!.Thickness = new Thickness(1, 0, 1, 0);

    _volumeLabel = new Label() {
      X = Pos.AnchorEnd(),
      Y = Pos.Absolute(1)
    };

    Reset();

    Add(
        _albumArtView,
        _volumeLabel,
        _songLabel,
        _artistLabel,
        _positionLabel,
        _progressBar,
        _durationLabel
    );

    _playbackQueueService.SongChanged += OnSongChanged;
    _playbackQueueService.PositionChanged += OnPositionChanged;
    _playbackQueueService.VolumeChanged += OnVolumeChanged;

    commandService.RegisterCommand("v", OnSetVolumeCommand);
    AddCommand(Command.HotKey, OnHotKey);
    HotKeyBindings.Add(Key.Space, this, Command.HotKey);
    HotKeyBindings.Add(Key.Space.WithCtrl, this, Command.HotKey);
    HotKeyBindings.Add(_previousKey, this, Command.HotKey);
    HotKeyBindings.Add(_nextKey, this, Command.HotKey);
  }

  protected override void Dispose(bool disposing) {
    _playbackQueueService.SongChanged -= OnSongChanged;
    _playbackQueueService.PositionChanged -= OnPositionChanged;
    _playbackQueueService.VolumeChanged -= OnVolumeChanged;
    base.Dispose(disposing);
  }

  private bool? OnHotKey(ICommandContext? ctx) {
    if (ctx?.Binding is KeyBinding keyBinding && keyBinding.Key is Key pressedKey) {
      if (pressedKey == Key.Space) {
        _ = _playbackQueueService.PlayPause();
      } else if (pressedKey == Key.Space.WithCtrl) {
        _playbackQueueService.Stop();
      } else if (pressedKey == _previousKey) {
        _ = _playbackQueueService.PreviousTrack();
      } else if (pressedKey == _nextKey) {
        _ = _playbackQueueService.NextTrack();
      }
    }
    return true;
  }

  private void OnSetVolumeCommand(string command, string args) {
    var splitArgs = CommandService.GetArgs(args);
    if (splitArgs.Length == 0) {
      return;
    }

    if (!int.TryParse(splitArgs[0], out int volume) || volume < 0 || volume > 100) {
      Logging.Warning($"Invalid volume: {splitArgs[0]}");
      _mainWindow.DisplayError($"invalid volume: {splitArgs[0]} ([0-100] expected)");
      return;
    }

    _playbackQueueService.Volume = volume / 100f;
  }

  private void OnVolumeChanged(object? sender, float e) {
    _volumeLabel.Text = string.Format(Messages.VOLUME, (int)Math.Round(e * 100));
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
    Logging.Information($"Song changed: {song?.Title ?? "(null)"}");
    if (song is not { }) {
      Reset();
      return;
    }

    _songLabel.Text = song.Title;
    _artistLabel.Text = song.Artist.Name;

    // Only bother downloading the album art if it has changed.
    if (song.Album.ThumbnailUrl is null || song.Album.ThumbnailUrl.Length == 0) {
      _albumArtView.ClearImage();
    } else if (_albumArtUrl != song.Album.ThumbnailUrl) {
      _albumArtUrl = song.Album.ThumbnailUrl;
      _albumArtCancellationTokenSource?.Cancel();
      _albumArtCancellationTokenSource = new CancellationTokenSource();
      var token = _albumArtCancellationTokenSource.Token;
      try {
        var albumResponse = await _httpClient.GetAsync(song.Album.ThumbnailUrl, token);
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

  private void Reset() {
    _songLabel.Text = Messages.NO_SONG;
    _artistLabel.Text = Messages.NO_ARTIST;
    _positionLabel.Text = "--:--";
    _durationLabel.Text = "--:--";
    _volumeLabel.Text = string.Format(Messages.VOLUME, (int)Math.Round(_playbackQueueService.Volume * 100));
    _progressBar.Fraction = 0.0f;
  }
}
