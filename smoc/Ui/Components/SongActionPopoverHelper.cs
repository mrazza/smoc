using System.Drawing;
using Smoc.Services;
using Smoc.Streaming;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Smoc.Ui.Components;

/// <summary>
/// Helper class for creating and managing the song action popover menu.
/// Provides consistent behavior across SongView and ArtistView.
/// </summary>
public sealed class SongActionPopoverHelper : IDisposable
{
    private static class Messages
    {
        public const string PLAY_ALL_FROM_HERE = "_play all from here";
        public const string PLAY_ONLY_THIS = "play _only this";
        public const string PLAY_NEXT = "play _next";
        public const string ADD_TO_QUEUE = "_add to queue";
    }

    private readonly PlayerService playerService;
    private readonly View parentView;
    private PopoverMenu? popover;

    public SongActionPopoverHelper(PlayerService playerService, View parentView)
    {
        this.playerService = playerService;
        this.parentView = parentView;
    }

    /// <summary>
    /// Shows the song action popover menu at the specified position.
    /// </summary>
    /// <param name="selectedSong">The currently selected song.</param>
    /// <param name="allSongs">All songs in the current view (for "Play All from Here").</param>
    /// <param name="selectedIndex">The index of the selected song within allSongs.</param>
    /// <param name="position">The screen position to show the popover at.</param>
    public void Show(Song selectedSong, IReadOnlyList<Song> allSongs, int selectedIndex, Point position)
    {
        if (popover is null)
        {
            popover = new PopoverMenu(
            [
                new MenuItem { Title = Messages.PLAY_ALL_FROM_HERE },
                new MenuItem { Title = Messages.PLAY_ONLY_THIS },
                new MenuItem { Title = Messages.PLAY_NEXT },
                new MenuItem { Title = Messages.ADD_TO_QUEUE }
            ]);
            parentView.App?.Popover?.Register(popover);
        }

        var root = popover.Root;
        if (root is null) return;

        var menuItems = root.SubViews.OfType<MenuItem>().ToList();
        if (menuItems.Count >= 4)
        {
            menuItems[0].Action = async () =>
            {
                try
                {
                    playerService.ClearPlaybackQueue();
                    playerService.QueueSongs(allSongs);
                    await playerService.ChangeTrack(selectedIndex);
                }
                catch (Exception ex)
                {
                    Logging.Error($"Error starting playback: {ex.Message}");
                }
            };
            menuItems[1].Action = async () =>
            {
                try
                {
                    playerService.ClearPlaybackQueue();
                    playerService.QueueSong(selectedSong);
                    await playerService.ChangeTrack(0);
                }
                catch (Exception ex)
                {
                    Logging.Error($"Error starting playback: {ex.Message}");
                }
            };
            menuItems[2].Action = () => playerService.InsertAfterCurrent(selectedSong);
            menuItems[3].Action = () => playerService.QueueSong(selectedSong);
        }

        popover.MakeVisible(position);
    }

    public void Dispose()
    {
        popover?.Dispose();
    }
}
