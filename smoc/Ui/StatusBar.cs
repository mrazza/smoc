using System.Reflection;
using Smoc.Services;
using Smoc.Streaming;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Smoc.Ui;

public sealed class StatusBar : View
{
    private static class Messages
    {
        public const string PLAY = "[PLAY]";
        public const string PAUSE = "[PAUSE]";
        public const string STOP = "[STOP]";
        public const string UNKNOWN = "[UNK]";
        public const string NO_SONG = "No song";
        public const string NO_ARTIST = "No artist";
    }

    private readonly Label modeLabel;
    private readonly Label versionLabel;
    private readonly Label stateLabel;
    private readonly PlayerService playerService;

    public StatusBar(PlayerService playerService)
    {
        this.playerService = playerService;
        Width = Dim.Fill();
        Height = Dim.Absolute(1);

        SetScheme(SchemeManager.GetScheme("StatusBar"));

        this.modeLabel = new Label()
        {
            Height = Dim.Fill()
        };
        this.versionLabel = new Label()
        {
            X = Pos.AnchorEnd(),
            Height = Dim.Fill(),
            Text = Program.PRODUCT_NAME + " v" + Assembly.GetEntryAssembly()!.GetName().Version!.ToString(3)
        };
        this.stateLabel = new Label()
        {
            X = Pos.Right(this.modeLabel),
            Width = Dim.Fill() - Dim.Func((view) => view!.Frame.Width, this.versionLabel),
            Height = Dim.Fill()
        };
        Terminal.Gui.Drawing.Scheme majorSectionScheme = SchemeManager.GetScheme("StatusBar_Mode");
        this.versionLabel.SetScheme(majorSectionScheme);
        this.modeLabel.SetScheme(majorSectionScheme);
        Thickness defaultMargin = new(1, 0, 1, 0);
        this.versionLabel.Padding!.Thickness = defaultMargin;
        this.modeLabel.Padding!.Thickness = defaultMargin;
        this.stateLabel.Padding!.Thickness = defaultMargin;
        Add(this.modeLabel, this.versionLabel, this.stateLabel);

        playerService.SongChanged += OnSongChanged;
        playerService.PositionChanged += OnPositionChanged;
        playerService.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    protected override void Dispose(bool disposing)
    {
        playerService.SongChanged -= OnSongChanged;
        playerService.PositionChanged -= OnPositionChanged;
        playerService.PlaybackStateChanged -= OnPlaybackStateChanged;
        base.Dispose(disposing);
    }

    private TimeSpan lastPosition;

    private void OnPositionChanged(object? sender, TimeSpan e)
    {
        if (e.Subtract(lastPosition) > TimeSpan.FromSeconds(1))
        {
            lastPosition = e;
            UpdateState();
        }
    }

    private void OnSongChanged(object? sender, Song e)
    {
        UpdateState();
    }

    private void OnPlaybackStateChanged(object? sender, PlaybackState e)
    {
        UpdateState();
    }

    private void UpdateState()
    {
        App?.Invoke(() =>
        {
            string playbackStatePrefix = playerService.PlaybackState switch
            {
                PlaybackState.Playing => Messages.PLAY,
                PlaybackState.Paused => Messages.PAUSE,
                PlaybackState.Stopped => Messages.STOP,
                _ => Messages.UNKNOWN
            };

            string songName = playerService.CurrentSong?.Title ?? Messages.NO_SONG;
            string artistName = playerService.CurrentSong?.Artist.Name ?? Messages.NO_ARTIST;
            string songDuration = playerService.Duration.ToString("mm\\:ss");
            string songPosition = playerService.CurrentTime.ToString("mm\\:ss");
            this.stateLabel.Text = $"{playbackStatePrefix} {artistName} - {songName} [{songPosition}/{songDuration}]";
        });
    }

    internal string GetMode()
    {
        return this.modeLabel.Text;
    }

    public void SetMode(string mode)
    {
        this.modeLabel.Text = mode;
    }

    public void SetState(string state)
    {
        this.stateLabel.Text = state;
    }
}