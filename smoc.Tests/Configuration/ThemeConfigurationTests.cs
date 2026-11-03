using Smoc.Ui;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Xunit;

namespace smoc.Tests.Configuration;

public class ThemeConfigurationTests : IDisposable {
  public ThemeConfigurationTests() {
    AppSchemes.ResetForTesting();
    TuiConfigurationBuilder.Shared.RuntimeConfig = null;
    ThemeManager.Theme = ThemeManager.DEFAULT_THEME_NAME;
    TuiConfigurationBuilder.Shared.ApplyToStaticFacades();
  }

  public void Dispose() {
    AppSchemes.ResetForTesting();
    TuiConfigurationBuilder.Shared.RuntimeConfig = null;
    ThemeManager.Theme = ThemeManager.DEFAULT_THEME_NAME;
    TuiConfigurationBuilder.Shared.ApplyToStaticFacades();
  }

  [Fact]
  public void UserOverrides_ThemeScopedScheme_TakesPrecedenceOverDefaults() {
    var builder = TuiConfigurationBuilder.Shared;
    builder.RuntimeConfig = """
    {
      "Theme": "CustomDark",
      "Themes": {
        "CustomDark": {
          "Schemes": {
            "Accent": {
              "Normal": { "Foreground": "#123456", "Background": "None" }
            },
            "ProgressBar": {
              "Normal": { "Foreground": "#654321", "Background": "#112233" }
            }
          }
        }
      }
    }
    """;
    builder.ApplyToStaticFacades();
    AppSchemes.RegisterDefaultSchemes();

    Assert.Equal("CustomDark", ThemeManager.Theme);

    Assert.True(SchemeManager.TryGetScheme("Accent", out var accentScheme));
    Assert.NotNull(accentScheme);
    Assert.Equal(new Color("#123456"), accentScheme.Normal.Foreground);

    Assert.True(SchemeManager.TryGetScheme("ProgressBar", out var progressBarScheme));
    Assert.NotNull(progressBarScheme);
    Assert.Equal(new Color("#654321"), progressBarScheme.Normal.Foreground);
    Assert.Equal(new Color("#112233"), progressBarScheme.Normal.Background);
  }

  [Fact]
  public void UserOverrides_RootScheme_TakesPrecedenceOverDefaults() {
    var builder = TuiConfigurationBuilder.Shared;
    builder.RuntimeConfig = """
    {
      "Schemes": {
        "Accent": {
          "Normal": { "Foreground": "#abcdef", "Background": "None" }
        },
        "StatusBar": {
          "Normal": { "Foreground": "#fedcba", "Background": "#121212" }
        }
      }
    }
    """;
    builder.ApplyToStaticFacades();
    AppSchemes.RegisterDefaultSchemes();

    Assert.True(SchemeManager.TryGetScheme("Accent", out var accentScheme));
    Assert.NotNull(accentScheme);
    Assert.Equal(new Color("#abcdef"), accentScheme.Normal.Foreground);

    Assert.True(SchemeManager.TryGetScheme("StatusBar", out var statusBarScheme));
    Assert.NotNull(statusBarScheme);
    Assert.Equal(new Color("#fedcba"), statusBarScheme.Normal.Foreground);
    Assert.Equal(new Color("#121212"), statusBarScheme.Normal.Background);
  }

  [Fact]
  public void PartialCustomization_FallbackToDefaultSchemes() {
    var builder = TuiConfigurationBuilder.Shared;
    builder.RuntimeConfig = """
    {
      "Theme": "CustomDark",
      "Themes": {
        "CustomDark": {
          "Schemes": {
            "Accent": {
              "Normal": { "Foreground": "#334455", "Background": "None" }
            }
          }
        }
      }
    }
    """;
    builder.ApplyToStaticFacades();
    AppSchemes.RegisterDefaultSchemes();

    // Overridden scheme should use custom value
    Assert.True(SchemeManager.TryGetScheme("Accent", out var accentScheme));
    Assert.NotNull(accentScheme);
    Assert.Equal(new Color("#334455"), accentScheme.Normal.Foreground);

    // Non-overridden schemes should fall back to SMoC default values
    Assert.True(SchemeManager.TryGetScheme("ProgressBar", out var progressBarScheme));
    Assert.NotNull(progressBarScheme);
    Assert.Equal(new Color("#ebdbb2"), progressBarScheme.Normal.Foreground);
    Assert.Equal(new Color("#394e4e"), progressBarScheme.Normal.Background);

    Assert.True(SchemeManager.TryGetScheme("TableCurrentTrack", out var tableCurrentTrackScheme));
    Assert.NotNull(tableCurrentTrackScheme);
    Assert.Equal(new Color("#ebdbb2"), tableCurrentTrackScheme.Normal.Foreground);
    Assert.Equal(new Color("#394e4e"), tableCurrentTrackScheme.Normal.Background);

    Assert.True(SchemeManager.TryGetScheme("TableHeaders", out var tableHeadersScheme));
    Assert.NotNull(tableHeadersScheme);
    Assert.Equal(new Color("#ebdbb2"), tableHeadersScheme.Normal.Foreground);

    Assert.True(SchemeManager.TryGetScheme("StatusBar", out var statusBarScheme));
    Assert.NotNull(statusBarScheme);
    Assert.Equal(new Color("#262626"), statusBarScheme.Normal.Foreground);
    Assert.Equal(new Color("#949494"), statusBarScheme.Normal.Background);

    Assert.True(SchemeManager.TryGetScheme("CommandLine", out var commandLineScheme));
    Assert.NotNull(commandLineScheme);
    Assert.Equal(new Color("#ebdbb2"), commandLineScheme.Normal.Foreground);

    Assert.True(SchemeManager.TryGetScheme("CommandLineError", out var commandLineErrorScheme));
    Assert.NotNull(commandLineErrorScheme);
    Assert.Equal(new Color("#262626"), commandLineErrorScheme.Normal.Foreground);
    Assert.Equal(new Color("#d75f5f"), commandLineErrorScheme.Normal.Background);
  }

  [Fact]
  public void ThemeSelection_AppliesSelectedThemeAndSchemes() {
    var builder = TuiConfigurationBuilder.Shared;
    builder.RuntimeConfig = """
    {
      "Theme": "NordicTheme",
      "Themes": {
        "NordicTheme": {
          "Schemes": {
            "Accent": {
              "Normal": { "Foreground": "#88c0d0", "Background": "#2e3440" }
            }
          }
        }
      }
    }
    """;
    builder.ApplyToStaticFacades();
    AppSchemes.RegisterDefaultSchemes();

    Assert.Equal("NordicTheme", ThemeManager.Theme);
    Assert.True(SchemeManager.TryGetScheme("Accent", out var accentScheme));
    Assert.NotNull(accentScheme);
    Assert.Equal(new Color("#88c0d0"), accentScheme.Normal.Foreground);
    Assert.Equal(new Color("#2e3440"), accentScheme.Normal.Background);
  }

  [Fact]
  public void ThemeChange_BackfillsDefaultSchemesOnNewTheme() {
    var builder = TuiConfigurationBuilder.Shared;
    builder.RuntimeConfig = """
    {
      "Theme": "ThemeOne",
      "Themes": {
        "ThemeOne": {
          "Schemes": {
            "Accent": {
              "Normal": { "Foreground": "#111111", "Background": "None" }
            }
          }
        },
        "ThemeTwo": {
          "Schemes": {
            "Accent": {
              "Normal": { "Foreground": "#222222", "Background": "None" }
            }
          }
        }
      }
    }
    """;
    builder.ApplyToStaticFacades();
    AppSchemes.RegisterDefaultSchemes();

    Assert.Equal("ThemeOne", ThemeManager.Theme);
    Assert.Equal(new Color("#111111"), SchemeManager.GetScheme("Accent").Normal.Foreground);
    Assert.Equal(new Color("#ebdbb2"), SchemeManager.GetScheme("ProgressBar").Normal.Foreground);

    // Switch theme to ThemeTwo at runtime
    ThemeManager.Theme = "ThemeTwo";

    Assert.Equal("ThemeTwo", ThemeManager.Theme);
    // ThemeTwo's custom Accent should be active
    Assert.Equal(new Color("#222222"), SchemeManager.GetScheme("Accent").Normal.Foreground);
    // SMoC default schemes should be backfilled for ThemeTwo
    Assert.True(SchemeManager.TryGetScheme("ProgressBar", out var progressBarScheme));
    Assert.NotNull(progressBarScheme);
    Assert.Equal(new Color("#ebdbb2"), progressBarScheme.Normal.Foreground);
    Assert.True(SchemeManager.TryGetScheme("StatusBar", out var statusBarScheme));
    Assert.NotNull(statusBarScheme);
    Assert.Equal(new Color("#262626"), statusBarScheme.Normal.Foreground);
  }
}
