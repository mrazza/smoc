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
      X = Pos.Absolute(1),
      Height = Dim.Absolute(1)
    };
    _versionLabel = new Label() {
      X = Pos.AnchorEnd(),
      Height = Dim.Absolute(1),
      Text = Program.ProductName + " v" + Assembly.GetEntryAssembly()!.GetName().Version!.ToString(3)
    };
    _stateLabel = new Label() {
      X = Pos.Right(_modeLabel),
      Height = Dim.Absolute(1),
      Width = Dim.Fill(to: _versionLabel),
    };
    _stateLabel.SetScheme(SchemeManager.GetScheme("StatusBar_State"));
    Thickness defaultMargin = new(1, 0, 1, 0);
    _versionLabel.Padding!.Thickness = defaultMargin;

    // HACK: RESOLVES RENDERING ISSUE ASSOCIATED WITH PADDING AND BACKGROUND
    // _modeLabel.Padding!.Thickness = defaultMargin;
    var leftSpacer = new View() {
      X = Pos.Absolute(0),
      Width = Dim.Absolute(1)
    };
    var rightModeSpacer = new View() {
      X = Pos.Right(_modeLabel),
      Width = Dim.Absolute(1)
    };
    _stateLabel.X = Pos.Right(rightModeSpacer);
    Add(leftSpacer, rightModeSpacer);
    // END HACK

    Add(_versionLabel, _modeLabel, _stateLabel);

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
    // HACK: RESOLVES RENDERING ISSUE ASSOCIATED WITH PADDING AND BACKGROUND
    //_stateLabel.Text = $"{playbackStatePrefix} {artistName} - {songName} [{songPosition}/{songDuration}]";
    _stateLabel.Text = $" {playbackStatePrefix} {artistName} - {songName} [{songPosition}/{songDuration}]";
    // END HACK
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
