using Terminal.Gui.Configuration;
using Terminal.Gui.Time;
using Terminal.Gui.ViewBase;

namespace smoc.Tests.TestInfra;

/// <summary>
/// Extension methods for <see cref="TerminalGuiFluentTesting.TestContext"/>.
/// </summary>
public static class ContextExtensions {
  private static volatile bool _isConfigSet = false;
  private static readonly object _configLock = new();

  /// <summary>
  /// Adds a view to the test context and waits for it to be laid out.
  /// </summary>
  /// <remarks>
  /// This is sometimes required if the view is incorrectly layed out initially to reduce flakiness.
  /// </remarks>
  /// <param name="context">The test context.</param>
  /// <param name="view">The view to add.</param>
  /// <returns>The test context.</returns>
  public static AppTestHelpers.AppTestHelper AddAndLayout(this AppTestHelpers.AppTestHelper context, View view) {
    return context.Add(view).Then((_) => view.SetNeedsLayout());
  }

  /// <summary>
  /// Advances the time in the test context.
  /// </summary>
  /// <param name="context">The test context.</param>
  /// <param name="timeSpan">The time to advance.</param>
  /// <returns>The test context.</returns>
  public static AppTestHelpers.AppTestHelper AdvanceTime(this AppTestHelpers.AppTestHelper context, TimeSpan timeSpan) {
    (context.TimeProvider as VirtualTimeProvider)?.Advance(timeSpan);
    return context.WaitIteration();
  }

  /// <summary>
  /// Configures the default theme for the test context.
  /// </summary>
  /// <remarks>
  /// This is required by some views to load themes and to display colors correctly.
  /// </remarks>
  /// <param name="context">The test context.</param>
  /// <returns>The test context.</returns>
  public static AppTestHelpers.AppTestHelper ConfigureDefaultTheme(this AppTestHelpers.AppTestHelper context) {
    if (_isConfigSet) return context;
    lock (_configLock) {
      if (_isConfigSet) return context;
      ConfigurationManager.RuntimeConfig = """
      {
        "Themes": [
            {
                "default": {
                    "Schemes": [
                        {
                            "CommandLine": {
                                "Normal": {
                                    "Foreground": "#ebdbb2",
                                    "Background": "#00000000"
                                }
                            }
                        },
                        {
                            "CommandLineError": {
                                "Normal": {
                                    "Foreground": "#262626",
                                    "Background": "#d75f5f",
                                    "Style": "Bold"
                                }
                            }
                        },
                        {
                            "ProgressBar": {
                                "Normal": {
                                    "Foreground": "#ebdbb2",
                                    "Background": "#394e4e"
                                }
                            }
                        },
                        {
                            "StatusBar_State": {
                                "Normal": {
                                    "Foreground": "#949494",
                                    "Background": "#3a3a3a"
                                }
                            }
                        },
                        {
                            "StatusBar": {
                                "Normal": {
                                    "Foreground": "#262626",
                                    "Background": "#949494",
                                    "Style": "Bold"
                                }
                            }
                        }
                    ]
                }
            }
        ]
    }
    """;
      ConfigurationManager.Enable(ConfigLocations.Runtime);
      _isConfigSet = true;
    }
    return context;
  }
}