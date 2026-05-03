using Smoc.Services.Audio.SoundFlow;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Smoc.Ui.Components;

/// <summary>
/// A ListView specialized for displaying search results, with custom key handling.
/// </summary>
public sealed class SearchResultsList<T> : ListView {
  // TODO: Extract this so that its shared with the actual command bindings
  private static readonly Key CommandKey = new Key(':').WithShift;

  /// <summary>
  /// Occurs when the user selects a search result.
  /// </summary>
  public event EventHandler<T>? SearchResultSelected;

  public SearchResultsList()
      : base() {
    VimKeyBindings.AddDirectionalKeyBindings(KeyBindings);

    Accepting += (_, args) => {
      if (SelectedItem is int itemId
        && Source is { }
        && Source.ToList()[itemId] is T item) {
        SearchResultSelected?.Invoke(this, item);

        args.Handled = true;
      }
    };
  }

  protected override bool OnKeyDown(Key key) {
    if (key == CommandKey) {
      return false;
    }

    return base.OnKeyDown(key);
  }
}
