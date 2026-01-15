using Smoc.Services;
using Smoc.Streaming;
using Smoc.Ui.Models;
using Terminal.Gui.ViewBase;

namespace Smoc.Ui;

public sealed class MainContent : View
{
    private readonly ArtistView artistView;
    private readonly PlayerView playerView;
    private readonly SongView songView;
    private readonly MainWindow mainWindow;

    private View? currentView;

    public MainContent(MainWindow mainWindow, CommandService commandService, PlayerService playerService, IStreamingClient streamingClient)
    {
        this.mainWindow = mainWindow;
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;
        currentView = null;

        artistView = new ArtistView(mainWindow, commandService, streamingClient, playerService)
        {
            Visible = false
        };
        playerView = new PlayerView(mainWindow, commandService, playerService)
        {
            Visible = false
        };
        songView = new SongView(mainWindow, commandService, streamingClient)
        {
            Visible = false
        };
        Add(artistView, playerView, songView);
    }

    public void SetMode(Mode mode)
    {
        if (currentView is not null)
        {
            currentView.Visible = false;
        }

        currentView = mode switch
        {
            Mode.Player => playerView,
            Mode.Artist => artistView,
            Mode.Song => songView,
            _ => throw new ArgumentException("Invalid mode"),
        };
        currentView!.Visible = true;
    }
}