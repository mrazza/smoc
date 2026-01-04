using Smoc.Services;
using Smoc.Streaming;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Smoc.Ui;

public sealed class NowPlaying : View
{
    private readonly PlayerService playerService;
    private readonly Label songLabel;
    private readonly Label artistLabel;
    private readonly Label positionLabel;
    private readonly ProgressBar progressBar;
    private readonly Label durationLabel;
    private readonly Label volumeLabel;

    public NowPlaying(PlayerService playerService, CommandService commandService)
    {
        this.playerService = playerService;
        Width = Dim.Fill();
        Height = Dim.Absolute(3);
        Padding!.Thickness = new Thickness(1, 0, 1, 0);

        this.songLabel = new Label()
        {
            X = Pos.Absolute(0),
            Y = Pos.Absolute(0)
        };

        this.artistLabel = new Label()
        {
            X = Pos.Absolute(0),
            Y = Pos.Absolute(1)
        };

        this.positionLabel = new Label()
        {
            X = Pos.Absolute(0),
            Y = Pos.Absolute(2)
        };

        this.durationLabel = new Label()
        {
            X = Pos.AnchorEnd(),
            Y = Pos.Absolute(2)
        };

        this.progressBar = new ProgressBar()
        {
            X = Pos.Right(this.positionLabel),
            Y = Pos.Absolute(2),
            Width = Dim.Fill() - Dim.Func((view) => view!.Frame.Width, durationLabel),
            ProgressBarStyle = ProgressBarStyle.Continuous
        };

        progressBar.Fraction = 0.5f;
        progressBar.Margin!.Thickness = new Thickness(1, 0, 1, 0);

        this.volumeLabel = new Label()
        {
            X = Pos.AnchorEnd(),
            Y = Pos.Absolute(1)
        };

        Reset();

        Add(
            this.volumeLabel,
            this.songLabel,
            this.artistLabel,
            this.positionLabel,
            this.progressBar,
            this.durationLabel
        );

        playerService.SongChanged += OnSongChanged;
        playerService.PositionChanged += OnPositionChanged;
        playerService.VolumeChanged += OnVolumeChanged;

        commandService.RegisterCommand("v", OnSetVolumeCommand);
        AddCommand(Command.HotKey, OnHotKey);
        HotKeyBindings.Add(Key.Space, this, Command.HotKey);
    }

    private bool? OnHotKey(ICommandContext? ctx)
    {
        playerService.PlayPause();
        return true;
    }

    private void OnSetVolumeCommand(string command, string args)
    {
        var splitArgs = CommandService.GetArgs(args);
        if (splitArgs.Length == 0)
        {
            return;
        }

        if (!int.TryParse(splitArgs[0], out int volume) || volume < 0 || volume > 100)
        {
            Logging.Warning($"Invalid volume: {args}");
            return;
        }

        playerService.Volume = volume / 100f;
    }

    private void OnVolumeChanged(object? sender, float e)
    {
        this.volumeLabel.Text = $"volume: {(int)Math.Round(e * 100)}%";
    }

    private void OnPositionChanged(object? sender, TimeSpan e)
    {
        this.positionLabel.Text = e.ToString("mm\\:ss");
        this.durationLabel.Text = playerService.Duration.ToString("mm\\:ss");
        this.progressBar.Fraction = (float)(e / playerService.Duration);
    }

    private void OnSongChanged(object? sender, Song e)
    {
        this.songLabel.Text = e.Title ?? "no song";
        this.artistLabel.Text = e.Artist.Name ?? "no artist";
    }

    private void Reset()
    {
        this.songLabel.Text = "no song";
        this.artistLabel.Text = "no artist";
        this.positionLabel.Text = "00:00";
        this.durationLabel.Text = "00:00";
        this.volumeLabel.Text = $"volume: {(int)Math.Round(playerService.Volume * 100)}%";
        this.progressBar.Fraction = 0.0f;
    }
}