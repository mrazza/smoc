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

  /// <inheritdoc/>
  public void SetMode(Mode mode) { CurrentMode = mode; }

  /// <inheritdoc/>
  public void DisplayError(string message) { }
}
