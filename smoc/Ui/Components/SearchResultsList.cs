using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Smoc.Ui.Components;

/// <summary>
/// A ListView specialized for displaying search results, with custom key handling.
/// </summary>
public sealed class SearchResultsList<T> : ListView {
  // TODO: Extract this so that its shared with the actual command bindings
  private static readonly Key[] _commandKeys = [new Key(':'), new Key(':').WithShift];

  /// <summary>
  /// Occurs when the user selects a search result.
  /// </summary>
  public event EventHandler<T>? SearchResultSelected;

  /// <summary>
  /// Initializes a new instance of the <see cref="SearchResultsList{T}"/> class.
  /// </summary>
  public SearchResultsList()
      : base() {
    VimKeyBindings.AddDirectionalKeyBindings(KeyBindings, bindLeftRight: false);
    VimKeyBindings.AddNavigationKeyBindings(KeyBindings, bindUpDown: false);

    Accepting += (_, args) => {
      if (SelectedItem is int itemId && itemId >= 0 && Source is { }) {
        var list = Source.ToList();
        if (itemId < list.Count && list[itemId] is T item) {
          SearchResultSelected?.Invoke(this, item);
          args.Handled = true;
        }
      }
    };
  }

  protected override bool OnKeyDown(Key key) {
    if (_commandKeys.Contains(key)) {
      return false;
    }

    return base.OnKeyDown(key);
  }
}
