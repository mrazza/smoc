using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Smoc.Ui.Components;

/// <summary>
/// A specialized TextField used for command input that does not
/// participate in standard tab navigation and prevents focus from advancing.
/// </summary>
public sealed class CommandTextField : TextField {
  public CommandTextField()
      : base() {
    CanFocus = true;
    TabStop = TabBehavior.NoStop;
    Cursor = new Cursor { Style = CursorStyle.Default };
  }

  protected override bool OnAdvancingFocus(NavigationDirection direction, TabBehavior? behavior) {
    return true;
  }
}
