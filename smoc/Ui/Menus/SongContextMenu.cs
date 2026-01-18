using Smoc.Services;
using Smoc.Streaming;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Smoc.Ui.Menus;

public sealed class SongContextMenu : PopoverMenu
{
    private static class MenuItems
    {
        public const string PLAY_ALL = "Play All From Here";
        public const string PLAY_SELECTION = "Play Selection Only";
        public const string PLAY_NEXT = "Play Next";
        public const string ADD_TO_QUEUE = "Add to Queue";
    }

    public SongContextMenu(PlayerService playerService, Song selectedSong, IEnumerable<Song>? contextSongs = null)
        : base(CreateMenuItems(playerService, selectedSong, contextSongs))
    {
        // Default PopoverMenu binds Left/Right for navigation (Bar behavior).
        // specific to Menu behavior we want Up/Down for vertical list.

        // Map Up to Previous (Backward)
        AddCommand(Command.Up, ctx => AdvanceFocus(NavigationDirection.Backward, TabBehavior.TabStop));
        KeyBindings.Add(Terminal.Gui.Input.Key.CursorUp, Command.Up);

        // Map Down to Next (Forward)
        AddCommand(Command.Down, ctx => AdvanceFocus(NavigationDirection.Forward, TabBehavior.TabStop));
        KeyBindings.Add(Terminal.Gui.Input.Key.CursorDown, Command.Down);

        // Map Esc to Cancel (Close)
        AddCommand(Command.Cancel, ctx =>
        {
            Visible = false;
            return true;
        });
        KeyBindings.Add(Terminal.Gui.Input.Key.Esc, Command.Cancel);
    }

    public int RequiredHeight => (Root?.SubViews.Count ?? 0) + 2;

    private static IEnumerable<MenuItem> CreateMenuItems(PlayerService playerService, Song selectedSong, IEnumerable<Song>? contextSongs)
    {
        var menuItems = new List<MenuItem>();
        var songsToPlay = contextSongs?.ToList() ?? new List<Song> { selectedSong };

        // 1. Play All From Here
        menuItems.Add(new MenuItem(MenuItems.PLAY_ALL, action: async () =>
        {
            playerService.ClearPlaybackQueue();
            playerService.QueueSongs(songsToPlay);
            await playerService.ChangeTrack(0);
        }));

        // 2. Play Selection Only
        menuItems.Add(new MenuItem(MenuItems.PLAY_SELECTION, action: async () =>
        {
            playerService.ClearPlaybackQueue();
            playerService.QueueSong(selectedSong);
            await playerService.ChangeTrack(0);
        }));

        // 3. Play Next
        menuItems.Add(new MenuItem(MenuItems.PLAY_NEXT, action: () =>
        {
            playerService.QueueNext(selectedSong);
        }));

        // 4. Add to Queue
        menuItems.Add(new MenuItem(MenuItems.ADD_TO_QUEUE, action: () =>
        {
            playerService.QueueLast(selectedSong);
        }));

        return menuItems;
    }

    protected override void OnVisibleChanged()
    {
        base.OnVisibleChanged();

        if (!Visible && IsInitialized)
        {
            App?.Popover?.DeRegister(this);
            Dispose();
        }
    }
}
