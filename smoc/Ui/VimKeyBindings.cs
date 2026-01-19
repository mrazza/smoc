using Terminal.Gui.Input;

namespace Smoc.Ui;

/// <summary>
/// Helper for registering Vim-style key bindings (hjkl).
/// </summary>
internal static class VimKeyBindings {
  /// <summary>
  /// Adds directional key bindings (Left/Right/Up/Down) mapped to hjkl.
  /// </summary>
  /// <param name="keyBinding">The KeyBindings collection to add to.</param>
  /// <param name="bindLeftRight">Whether to bind h/l to left/right.</param>
  /// <param name="bindUpDown">Whether to bind j/k to down/up.</param>
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

  /// <summary>
  /// Adds navigation key bindings (Next/Prev TabStop) mapped to hjkl.
  /// </summary>
  /// <param name="keyBinding">The KeyBindings collection to add to.</param>
  /// <param name="bindLeftRight">Whether to bind h/l to prev/next tab stop.</param>
  /// <param name="bindUpDown">Whether to bind j/k to prev/next tab stop.</param>
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
