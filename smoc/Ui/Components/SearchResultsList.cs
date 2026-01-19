using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Smoc.Ui.Components;

/// <summary>
/// A ListView specialized for displaying search results, with custom key handling.
/// </summary>
public sealed class SearchResultsList : ListView {
  // TODO: Extract this so that its shared with the actual command bindings
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
