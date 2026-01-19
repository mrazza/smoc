using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui.Components;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
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

  private readonly MainWindow _mainWindow;
  private readonly PlayerService _playerService;
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
  /// <param name="playerService">The player service for playback information.</param>
  /// <param name="commandService">The command service for registering volume commands.</param>
  public NowPlaying(MainWindow mainWindow, PlayerService playerService, CommandService commandService) {
    _mainWindow = mainWindow;
    _playerService = playerService;
    _httpClient = new HttpClient();
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

    playerService.SongChanged += OnSongChanged;
    playerService.PositionChanged += OnPositionChanged;
    playerService.VolumeChanged += OnVolumeChanged;
    playerService.PlaybackStateChanged += OnPlaybackStateChanged;

    commandService.RegisterCommand("v", OnSetVolumeCommand);
    AddCommand(Command.HotKey, OnHotKey);
    HotKeyBindings.Add(Key.Space, this, Command.HotKey);
  }

  protected override void Dispose(bool disposing) {
    _playerService.SongChanged -= OnSongChanged;
    _playerService.PositionChanged -= OnPositionChanged;
    _playerService.VolumeChanged -= OnVolumeChanged;
    _playerService.PlaybackStateChanged -= OnPlaybackStateChanged;
    _httpClient.Dispose();
    base.Dispose(disposing);
  }

  private bool? OnHotKey(ICommandContext? ctx) {
    _ = _playerService.PlayPause();
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

    _playerService.Volume = volume / 100f;
  }

  private void OnVolumeChanged(object? sender, float e) {
    _volumeLabel.Text = string.Format(Messages.VOLUME, (int)Math.Round(e * 100));
  }

  private void OnPositionChanged(object? sender, TimeSpan e) {
    _positionLabel.Text = e.ToString("mm\\:ss");
    _durationLabel.Text = _playerService.Duration.ToString("mm\\:ss");

    var progress = (float)Math.Round(e / _playerService.Duration, 2);
    if (_progressBar.Fraction != progress) {
      _progressBar.Fraction = progress;
    }
  }

  private void OnPlaybackStateChanged(object? sender, PlaybackState e) {
    if (e == PlaybackState.Playing || e == PlaybackState.Paused) {
      OnSongChanged(sender, _playerService.CurrentSong!);
    }
  }

  private async void OnSongChanged(object? sender, Song e) {
    Logging.Information($"Song changed: {e.Title}");
    _songLabel.Text = e.Title ?? Messages.NO_SONG;
    _artistLabel.Text = e.Artist.Name ?? Messages.NO_ARTIST;

    // Only bother downloading the album art if it has changed.
    if (e.Album.ThumbnailUrl is null || e.Album.ThumbnailUrl.Length == 0) {
      _albumArtView.ClearImage();
    }
    else if (_albumArtUrl != e.Album.ThumbnailUrl) {
      _albumArtUrl = e.Album.ThumbnailUrl;
      _albumArtCancellationTokenSource?.Cancel();
      _albumArtCancellationTokenSource = new CancellationTokenSource();
      var token = _albumArtCancellationTokenSource.Token;
      var albumResponse = await _httpClient.GetAsync(e.Album.ThumbnailUrl, token);
      var image = Image.Load<Rgba32>(albumResponse.Content.ReadAsStream());
      Logging.Debug($"Album art loaded: {e.Title}");
      token.ThrowIfCancellationRequested();
      _albumArtView.SetImage(image);
    }
  }

  private void Reset() {
    _songLabel.Text = Messages.NO_SONG;
    _artistLabel.Text = Messages.NO_ARTIST;
    _positionLabel.Text = "--:--";
    _durationLabel.Text = "--:--";
    _volumeLabel.Text = string.Format(Messages.VOLUME, (int)Math.Round(_playerService.Volume * 100));
    _progressBar.Fraction = 0.0f;
  }
}
