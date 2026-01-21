using Terminal.Gui.App;
using Terminal.Gui.ViewBase;

namespace smoc.Tests.TestInfra;

public static class ContextExtensions {
  /// <summary>
  /// Adds a view to the test context and waits for it to be laid out.
  /// </summary>
  /// <remarks>
  /// This is sometimes required if the view is incorrectly layed out initially to reduce flakiness.
  /// </remarks>
  /// <param name="context">The test context.</param>
  /// <param name="view">The view to add.</param>
  /// <returns>The test context.</returns>
  public static TerminalGuiFluentTesting.TestContext AddAndLayout(this TerminalGuiFluentTesting.TestContext context, View view) {
    return context.Add(view).Then((_) => view.SetNeedsLayout());
  }
}