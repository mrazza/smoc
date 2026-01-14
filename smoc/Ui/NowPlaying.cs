using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui.Components;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Color = Terminal.Gui.Drawing.Color;

namespace Smoc.Ui;

public sealed class NowPlaying : View
{
    private static class Messages
    {
        public const string NO_SONG = "no song";
        public const string NO_ARTIST = "no artist";
        public const string VOLUME = "volume: {0}%";
    }

    private readonly PlayerService playerService;
    private string? albumArtUrl;
    private readonly SixelImageView albumArtView;
    private readonly Label songLabel;
    private readonly Label artistLabel;
    private readonly Label positionLabel;
    private readonly ProgressBar progressBar;
    private readonly Label durationLabel;
    private readonly Label volumeLabel;
    private readonly HttpClient httpClient;

    public NowPlaying(PlayerService playerService, CommandService commandService)
    {
        this.playerService = playerService;
        this.httpClient = new HttpClient();
        this.albumArtUrl = null;
        Width = Dim.Fill();
        Height = Dim.Absolute(3);
        Padding!.Thickness = new Thickness(1, 0, 1, 0);
        CanFocus = false;

        this.albumArtView = new SixelImageView()
        {
            X = Pos.Absolute(0),
            Y = Pos.Absolute(0),
            Width = Dim.Absolute(6),
            Height = Dim.Fill(),
            BorderStyle = LineStyle.Dashed
        };
        albumArtView.Margin!.Thickness = new Thickness(0, 0, 1, 0);

        this.songLabel = new Label()
        {
            X = Pos.Right(this.albumArtView),
            Y = Pos.Absolute(0)
        };

        this.artistLabel = new Label()
        {
            X = Pos.Right(this.albumArtView),
            Y = Pos.Absolute(1)
        };

        this.positionLabel = new Label()
        {
            X = Pos.Right(this.albumArtView),
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
            this.albumArtView,
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

    protected override void Dispose(bool disposing)
    {
        playerService.SongChanged -= OnSongChanged;
        playerService.PositionChanged -= OnPositionChanged;
        playerService.VolumeChanged -= OnVolumeChanged;
        httpClient.Dispose();
        base.Dispose(disposing);
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
        this.volumeLabel.Text = string.Format(Messages.VOLUME, (int)Math.Round(e * 100));
    }

    private void OnPositionChanged(object? sender, TimeSpan e)
    {
        this.positionLabel.Text = e.ToString("mm\\:ss");
        this.durationLabel.Text = playerService.Duration.ToString("mm\\:ss");

        var progress = (float)Math.Round(e / playerService.Duration, 2);
        if (this.progressBar.Fraction != progress)
        {
            this.progressBar.Fraction = progress;
        }
    }

    private void OnSongChanged(object? sender, Song e)
    {
        Logging.Information($"Song changed: {e.Title}");
        this.songLabel.Text = e.Title ?? Messages.NO_SONG;
        this.artistLabel.Text = e.Artist.Name ?? Messages.NO_ARTIST;

        // Only bother downloading the album art if it has changed.
        if (e.Album.ThumbnailUrl is not null && albumArtUrl != e.Album.ThumbnailUrl)
        {
            albumArtUrl = e.Album.ThumbnailUrl;
            httpClient.GetAsync(e.Album.ThumbnailUrl).ContinueWith((task) =>
            {
                var image = Image.Load<Rgba32>(task.Result.Content.ReadAsStream());
                Logging.Information($"Album art loaded: {e.Title}");
                this.albumArtView.SetImage(image);
            });
        }
    }

    private void Reset()
    {
        this.songLabel.Text = Messages.NO_SONG;
        this.artistLabel.Text = Messages.NO_ARTIST;
        this.positionLabel.Text = "00:00";
        this.durationLabel.Text = "00:00";
        this.volumeLabel.Text = string.Format(Messages.VOLUME, (int)Math.Round(playerService.Volume * 100));
        this.progressBar.Fraction = 0.0f;
    }
}