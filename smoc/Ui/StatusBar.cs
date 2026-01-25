using System.Reflection;
using Smoc.Services;
using Smoc.Streaming;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Smoc.Ui;

/// <summary>
/// Displays application status, including mode, version, and playback state.
/// </summary>
public sealed class StatusBar : View {
  private static class Messages {
    public const string PLAY = "[PLAY]";
    public const string PAUSE = "[PAUSE]";
    public const string STOP = "[STOP]";
    public const string UNKNOWN = "[UNK]";
    public const string NO_SONG = "no track";
    public const string NO_ARTIST = "no artist";
  }

  private readonly Label _modeLabel;
  private readonly Label _versionLabel;
  private readonly Label _stateLabel;
  private readonly IPlaybackQueueService _playbackQueueService;

  /// <summary>
  /// Initializes a new instance of the <see cref="StatusBar"/> class.
  /// </summary>
  /// <param name="playbackQueueService">The player service to monitor.</param>
  public StatusBar(IPlaybackQueueService playbackQueueService) {
    _playbackQueueService = playbackQueueService;
    Width = Dim.Fill();
    Height = Dim.Absolute(1);

    SetScheme(SchemeManager.GetScheme("StatusBar"));

    _modeLabel = new Label() {
      Height = Dim.Fill()
    };
    _versionLabel = new Label() {
      X = Pos.AnchorEnd(),
      Height = Dim.Fill(),
      Text = Program.ProductName + " v" + Assembly.GetEntryAssembly()!.GetName().Version!.ToString(3)
    };
    _stateLabel = new Label() {
      X = Pos.Right(_modeLabel),
      Width = Dim.Fill() - Dim.Func((view) => view!.Frame.Width, _versionLabel),
      Height = Dim.Fill()
    };
    var majorSectionScheme = SchemeManager.GetScheme("StatusBar_Mode");
    _versionLabel.SetScheme(majorSectionScheme);
    _modeLabel.SetScheme(majorSectionScheme);
    Thickness defaultMargin = new(1, 0, 1, 0);
    _versionLabel.Padding!.Thickness = defaultMargin;
    _modeLabel.Padding!.Thickness = defaultMargin;
    _stateLabel.Padding!.Thickness = defaultMargin;
    Add(_modeLabel, _versionLabel, _stateLabel);

    _playbackQueueService.SongChanged += OnSongChanged;
    _playbackQueueService.PositionChanged += OnPositionChanged;
    _playbackQueueService.PlaybackStateChanged += OnPlaybackStateChanged;
  }

  protected override void Dispose(bool disposing) {
    _playbackQueueService.SongChanged -= OnSongChanged;
    _playbackQueueService.PositionChanged -= OnPositionChanged;
    _playbackQueueService.PlaybackStateChanged -= OnPlaybackStateChanged;
    base.Dispose(disposing);
  }

  private void OnPositionChanged(object? sender, TimeSpan e) {
    UpdateState();
  }

  private void OnSongChanged(object? sender, Song? song) {
    UpdateState();
  }

  private void OnPlaybackStateChanged(object? sender, PlaybackState e) {
    UpdateState();
  }

  private void UpdateState() {
    string playbackStatePrefix = _playbackQueueService.PlaybackState switch {
      PlaybackState.Playing => Messages.PLAY,
      PlaybackState.Paused => Messages.PAUSE,
      PlaybackState.Stopped => Messages.STOP,
      _ => Messages.UNKNOWN
    };

    string songName = _playbackQueueService.CurrentSong?.Title ?? Messages.NO_SONG;
    string artistName = _playbackQueueService.CurrentSong?.Artist.Name ?? Messages.NO_ARTIST;
    string songDuration = _playbackQueueService.Duration.ToString("mm\\:ss");
    string songPosition = _playbackQueueService.CurrentTime.ToString("mm\\:ss");
    _stateLabel.Text = $"{playbackStatePrefix} {artistName} - {songName} [{songPosition}/{songDuration}]";
  }

  internal string GetMode() {
    return _modeLabel.Text;
  }

  /// <summary>
  /// Sets the displayed mode text.
  /// </summary>
  /// <param name="mode">The mode name to display.</param>
  public void SetMode(string mode) {
    _modeLabel.Text = mode;
  }

  /// <summary>
  /// Sets the displayed state text manually.
  /// </summary>
  /// <param name="state">The state text.</param>
  public void SetState(string state) {
    _stateLabel.Text = state;
  }
}
