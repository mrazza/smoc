using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Smoc.Ui.Components;

public sealed class CommandTextField : TextField
{
    public CommandTextField()
        : base()
    {
        CanFocus = true;
        TabStop = TabBehavior.NoStop;
        Cursor = new Cursor { Style = CursorStyle.Default };
    }

    protected override bool OnAdvancingFocus(NavigationDirection direction, TabBehavior? behavior)
    {
        return true;
    }
}