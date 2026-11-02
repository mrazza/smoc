using Smoc.Ui;
using Smoc.Ui.Models;
using Terminal.Gui.App;

namespace smoc.Tests.Fakes;

/// <summary>
/// A fake implementation of <see cref="MainWindow"/> for use in tests.
/// </summary>
public class FakeMainWindow : IMainWindow {

  /// <inheritdoc/>
  public IApplication? App { get; } = FakeApplication.New();

  /// <inheritdoc/>
  public Mode CurrentMode { get; set; }

  /// <summary>
  /// Gets the list of error messages displayed via <see cref="DisplayError"/>.
  /// </summary>
  public List<string> DisplayedErrors { get; } = [];

  /// <summary>
  /// Gets the last error message displayed via <see cref="DisplayError"/>, or null if none.
  /// </summary>
  public string? LastDisplayedError => DisplayedErrors.LastOrDefault();

  /// <inheritdoc/>
  public void SetMode(Mode mode) { CurrentMode = mode; }

  /// <inheritdoc/>
  public void DisplayError(string message) {
    DisplayedErrors.Add(message);
  }
}
