using Terminal.Gui.Input;

namespace Smoc.Ui;

internal static class VimKeyBindings
{
    public static void AddDirectionalKeyBindings(KeyBindings keyBinding, bool bindLeftRight = true, bool bindUpDown = true)
    {
        if (bindLeftRight)
        {
            keyBinding.Add(Key.H, Command.Left);
            keyBinding.Add(Key.L, Command.Right);
        }

        if (bindUpDown)
        {
            keyBinding.Add(Key.J, Command.Down);
            keyBinding.Add(Key.K, Command.Up);
        }
    }

    public static void AddNavigationKeyBindings(KeyBindings keyBinding, bool bindLeftRight = true, bool bindUpDown = true)
    {
        if (bindLeftRight)
        {
            keyBinding.ReplaceCommands(Key.L, Command.NextTabStop);
            keyBinding.ReplaceCommands(Key.H, Command.PreviousTabStop);
        }

        if (bindUpDown)
        {
            keyBinding.ReplaceCommands(Key.J, Command.NextTabStop);
            keyBinding.ReplaceCommands(Key.K, Command.PreviousTabStop);
        }
    }
}