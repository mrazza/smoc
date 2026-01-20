using Smoc.Ui.Models;

namespace Smoc.Ui;

/// <summary>
/// Interface for functionality exposed by the main window.
/// </summary>
public interface IMainWindow {
  /// <summary>
  /// Changes the application's current mode (view).
  /// </summary>
  /// <param name="mode">The mode to switch to.</param>
  void SetMode(Mode mode);

  /// <summary>
  /// Displays a temporary error message to the user.
  /// </summary>
  /// <param name="message">The message to display.</param>
  void DisplayError(string message);
}