using Smoc.Ui;
using Smoc.Ui.Components;
using Smoc.Ui.Models;

namespace smoc.Tests.Fakes;

/// <summary>
/// A fake implementation of <see cref="MainWindow"/> for use in tests.
/// </summary>
public class FakeMainWindow : IMainWindow {
  /// <inheritdoc/>
  public Mode CurrentMode { get; set; }

  /// <inheritdoc/>
  public void SetMode(Mode mode) { CurrentMode = mode; }

  /// <summary>
  /// Empty implementation of <see cref="IMainWindow.DisplayError(string)"/> which does nothing.
  /// </summary>
  /// <param name="message">The error message.</param>
  public void DisplayError(string message) { }
}
