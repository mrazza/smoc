namespace Smoc.Ui.Models;

/// <summary>
/// Wraps a search result item for display in a ListView.
/// </summary>
/// <typeparam name="T">The type of the search result item.</typeparam>
/// <param name="Item">The underlying result item.</param>
/// <param name="DisplayText">The text to display in the list.</param>
internal record SearchResultRow<T>(T Item, string DisplayText) {
  public override string ToString() => DisplayText;
}
