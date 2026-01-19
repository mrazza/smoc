using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Smoc.Ui.Components;

public sealed class SearchResultsList : ListView {
  private static readonly Key CommandKey = new Key(':');

  public SearchResultsList()
      : base() {
    VimKeyBindings.AddDirectionalKeyBindings(KeyBindings);
  }

  protected override bool OnKeyDown(Key key) {
    if (key == CommandKey) {
      return false;
    }

    return base.OnKeyDown(key);
  }
}
