using Terminal.Gui.Input;

namespace Smoc.Ui;

internal static class VimKeyBindings {
  public static void AddDirectionalKeyBindings(KeyBindings keyBinding, bool bindLeftRight = true, bool bindUpDown = true) {
    if (bindLeftRight) {
      keyBinding.Add(Key.H, Command.Left);
      keyBinding.Add(Key.H.WithShift, Command.LeftExtend);
      keyBinding.Add(Key.L, Command.Right);
      keyBinding.Add(Key.L.WithShift, Command.RightExtend);
    }

    if (bindUpDown) {
      keyBinding.Add(Key.J, Command.Down);
      keyBinding.Add(Key.J.WithShift, Command.DownExtend);
      keyBinding.Add(Key.K, Command.Up);
      keyBinding.Add(Key.K.WithShift, Command.UpExtend);
    }
  }

  public static void AddNavigationKeyBindings(KeyBindings keyBinding, bool bindLeftRight = true, bool bindUpDown = true) {
    if (bindLeftRight) {
      keyBinding.ReplaceCommands(Key.L, Command.NextTabStop);
      keyBinding.ReplaceCommands(Key.H, Command.PreviousTabStop);
    }

    if (bindUpDown) {
      keyBinding.ReplaceCommands(Key.J, Command.NextTabStop);
      keyBinding.ReplaceCommands(Key.K, Command.PreviousTabStop);
    }
  }
}
