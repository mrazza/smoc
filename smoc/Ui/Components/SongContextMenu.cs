using Smoc.Services;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Smoc.Ui.Components;

public sealed class SongContextMenu : PopoverMenu
{
    private static class Messages
    {
        public const string PLAY_ALL = "_play all from here";
        public const string PLAY_SELECTION = "play selection _only";
        public const string PLAY_NEXT = "queue _next";
        public const string ADD_TO_QUEUE = "_queue last";
    }

    public SongContextMenu(PlayerService playerService, SongTable songTable)
        : base(CreateMenuItems(playerService, songTable))
    {
        // Default PopoverMenu binds Left/Right for navigation (Bar behavior).
        // specific to Menu behavior we want Up/Down for vertical list.
        // Map Up to Previous (Backward)
        AddCommand(Command.Up, ctx => AdvanceFocus(NavigationDirection.Backward, TabBehavior.TabStop));
        KeyBindings.Add(Terminal.Gui.Input.Key.CursorUp, Command.Up);
        // Map Down to Next (Forward)
        AddCommand(Command.Down, ctx => AdvanceFocus(NavigationDirection.Forward, TabBehavior.TabStop));
        KeyBindings.Add(Terminal.Gui.Input.Key.CursorDown, Command.Down);
        VimKeyBindings.AddDirectionalKeyBindings(KeyBindings);

        // Map Esc to Cancel (Close)
        AddCommand(Command.Cancel, ctx =>
        {
            Visible = false;
            return true;
        });
        KeyBindings.Add(Terminal.Gui.Input.Key.Esc, Command.Cancel);
    }

    public override void EndInit()
    {
        base.EndInit();

        App!.Popover?.Register(this);
    }

    public int RequiredHeight => Root?.SubViews.Count ?? 0;

    private static IEnumerable<MenuItem> CreateMenuItems(PlayerService playerService, SongTable songTable)
    {
        var menuItems = new List<MenuItem>();

        menuItems.Add(new MenuItem(Messages.PLAY_ALL, action: async () =>
        {
            playerService.ClearPlaybackQueue();
            playerService.QueueSongs(songTable.GetSongs());
            await playerService.ChangeTrack(songTable.SelectedRow);
        }));

        menuItems.Add(new MenuItem(Messages.PLAY_SELECTION, action: async () =>
        {
            playerService.ClearPlaybackQueue();
            playerService.QueueSongs(songTable.GetSelectedSongs());
            await playerService.ChangeTrack(0);
        }));

        menuItems.Add(new MenuItem(Messages.PLAY_NEXT, action: () =>
        {
            playerService.QueueNext(songTable.GetSelectedSongs());
        }));

        menuItems.Add(new MenuItem(Messages.ADD_TO_QUEUE, action: () =>
        {
            playerService.QueueLast(songTable.GetSelectedSongs());
        }));

        return menuItems;
    }

    protected override void OnAccepted(CommandEventArgs args)
    {
        base.OnAccepted(args);
        Visible = false;
    }

    protected override void Dispose(bool disposing)
    {
        App?.Popover?.DeRegister(this);
        base.Dispose(disposing);
    }
}
