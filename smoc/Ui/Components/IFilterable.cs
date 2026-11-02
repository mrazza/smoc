namespace Smoc.Ui.Components;

/// <summary>
/// Interface for UI views or components that support text filtering.
/// </summary>
public interface IFilterable {
  /// <summary>
  /// Applies a text filter to the view.
  /// </summary>
  /// <param name="filterText">The filter text to apply.</param>
  void ApplyFilter(string? filterText);

  /// <summary>
  /// Clears any active filter on the view.
  /// </summary>
  void ClearFilter();
}
