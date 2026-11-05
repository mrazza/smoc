using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Smoc.Ui.Components;

/// <summary>
/// A ListView specialized for displaying search results, with custom key handling.
/// </summary>
/// <typeparam name="T">The type of items displayed in the list.</typeparam>
public sealed class SearchResultsList<T> : ListView {
  private static readonly HashSet<Key> _ignoredKeys = [
    ..MainWindow.CommandHotKeys,
    ..NowPlayingBar.PlaybackHotKeys,
  ];

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
    KeyBindings.Remove(Key.Space);

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

  /// <inheritdoc />
  protected override bool OnKeyDown(Key key) {
    if (_ignoredKeys.Contains(key)) {
      return false;
    }

    return base.OnKeyDown(key);
  }
}
