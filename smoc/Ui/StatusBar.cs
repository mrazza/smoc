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

        playerService.SongChanged += (_, __) => UpdateState();
        playerService.PositionChanged += (_, __) => UpdateState();
        playerService.PlaybackStateChanged += (_, __) => UpdateState();
    }

    private void UpdateState()
    {
        App?.Invoke(() =>
        {
            string playbackStatePrefix = playerService.PlaybackState switch
            {
                PlaybackState.Playing => "[PLAY]",
                PlaybackState.Paused => "[PAUSE]",
                PlaybackState.Stopped => "[STOP]",
                _ => "[UNK]"
            };

            string songName = playerService.CurrentSong?.Title ?? "No song";
            string artistName = playerService.CurrentSong?.Artist.Name ?? "No artist";
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