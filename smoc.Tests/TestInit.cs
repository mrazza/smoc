using System.Runtime.CompilerServices;

/// <summary>
/// Initializes the test environment.
/// </summary>
internal static class TestInitializer {
  /// <summary>
  /// Sets up the test environment.
  /// </summary>
  [ModuleInitializer]
  public static void SetupEnvironment() {
    // Disable real driver IO to prevent terminal.gui from opening a real terminal.
    Environment.SetEnvironmentVariable("DisableRealDriverIO", "1");
  }
}