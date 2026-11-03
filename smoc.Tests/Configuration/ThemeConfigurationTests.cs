using System.Text.Json.Nodes;
using Smoc;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Xunit;

namespace smoc.Tests.Configuration;

[CollectionDefinition("ThemeConfigurationTests", DisableParallelization = true)]
public class ThemeConfigurationTestCollection { }

[Collection("ThemeConfigurationTests")]
public class ThemeConfigurationTests : IDisposable {
  private static readonly string _appDefaultsJson = LoadAppDefaultsJson();

  public ThemeConfigurationTests() {
    ResetConfiguration();
  }

  public void Dispose() {
    ResetConfiguration();
    smoc.Tests.TestInfra.ContextExtensions.EnsureDefaultTestSchemes();
  }

  private static string LoadAppDefaultsJson() {
    var names = typeof(Program).Assembly.GetManifestResourceNames();
    var resourceName = names.First(n => n.EndsWith("config.json"));
    using var stream = typeof(Program).Assembly.GetManifestResourceStream(resourceName)!;
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
  }

  private static void ResetConfiguration() {
    TuiConfigurationBuilder.Shared.RuntimeConfig = null;
    ThemeManager.Theme = ThemeManager.DEFAULT_THEME_NAME;
    TuiConfigurationBuilder.Shared.ApplyToStaticFacades();
  }

  private static void ApplyConfigWithDefaults(string? userJson = null) {
    var merged = MergeJson(_appDefaultsJson, userJson);
    var builder = TuiConfigurationBuilder.Shared;
    builder.RuntimeConfig = merged;
    builder.ApplyToStaticFacades();
  }

  private static string MergeJson(string baseJson, string? overlayJson) {
    if (string.IsNullOrWhiteSpace(overlayJson)) return baseJson;
    var baseNode = JsonNode.Parse(baseJson)!.AsObject();
    var overlayNode = JsonNode.Parse(overlayJson)!.AsObject();
    DeepMerge(baseNode, overlayNode);
    return baseNode.ToJsonString();
  }

  private static void DeepMerge(JsonObject target, JsonObject source) {
    foreach (var (key, value) in source) {
      if (value is JsonObject sourceChild && target.TryGetPropertyValue(key, out var targetChild) && targetChild is JsonObject targetObj) {
        DeepMerge(targetObj, sourceChild.DeepClone().AsObject());
      } else {
        target[key] = value?.DeepClone();
      }
    }
  }

  [Fact]
  public void ResourceExistsInSmocAssembly() {
    Assert.False(string.IsNullOrWhiteSpace(_appDefaultsJson));
    Assert.Contains("Accent", _appDefaultsJson);
    Assert.Contains("ProgressBar", _appDefaultsJson);
    Assert.Contains("TableCurrentTrack", _appDefaultsJson);
    Assert.Contains("TableHeaders", _appDefaultsJson);
    Assert.Contains("TableNormalTracks", _appDefaultsJson);
    Assert.Contains("Menu", _appDefaultsJson);
    Assert.Contains("StatusBar", _appDefaultsJson);
    Assert.Contains("StatusBar_State", _appDefaultsJson);
    Assert.Contains("CommandLine", _appDefaultsJson);
    Assert.Contains("CommandLineError", _appDefaultsJson);
  }

  [Fact]
  public void Defaults_LoadedFromEmbeddedResource() {
    ApplyConfigWithDefaults();

    Assert.True(SchemeManager.TryGetScheme("Accent", out var accent));
    Assert.NotNull(accent);
    Assert.Equal(new Color("#ebdbb2"), accent.Normal.Foreground);
    Assert.Equal(new Color("#639494"), accent.Focus.Background);

    Assert.True(SchemeManager.TryGetScheme("ProgressBar", out var progressBar));
    Assert.NotNull(progressBar);
    Assert.Equal(new Color("#ebdbb2"), progressBar.Normal.Foreground);
    Assert.Equal(new Color("#394e4e"), progressBar.Normal.Background);

    Assert.True(SchemeManager.TryGetScheme("TableCurrentTrack", out var tableCurrentTrack));
    Assert.NotNull(tableCurrentTrack);
    Assert.Equal(new Color("#ebdbb2"), tableCurrentTrack.Normal.Foreground);
    Assert.Equal(new Color("#394e4e"), tableCurrentTrack.Normal.Background);

    Assert.True(SchemeManager.TryGetScheme("TableHeaders", out var tableHeaders));
    Assert.NotNull(tableHeaders);
    Assert.Equal(new Color("#ebdbb2"), tableHeaders.Normal.Foreground);

    Assert.True(SchemeManager.TryGetScheme("TableNormalTracks", out var tableNormalTracks));
    Assert.NotNull(tableNormalTracks);
    Assert.Equal(new Color("#ebdbb2"), tableNormalTracks.Normal.Foreground);
    Assert.Equal(new Color("#639494"), tableNormalTracks.Focus.Background);

    Assert.True(SchemeManager.TryGetScheme("Menu", out var menu));
    Assert.NotNull(menu);
    Assert.Equal(new Color("#ebdbb2"), menu.Normal.Foreground);
    Assert.Equal(new Color("#3a3a3a"), menu.Normal.Background);

    Assert.True(SchemeManager.TryGetScheme("StatusBar", out var statusBar));
    Assert.NotNull(statusBar);
    Assert.Equal(new Color("#262626"), statusBar.Normal.Foreground);
    Assert.Equal(new Color("#949494"), statusBar.Normal.Background);

    Assert.True(SchemeManager.TryGetScheme("StatusBar_State", out var statusBarState));
    Assert.NotNull(statusBarState);
    Assert.Equal(new Color("#949494"), statusBarState.Normal.Foreground);
    Assert.Equal(new Color("#3a3a3a"), statusBarState.Normal.Background);

    Assert.True(SchemeManager.TryGetScheme("CommandLine", out var commandLine));
    Assert.NotNull(commandLine);
    Assert.Equal(new Color("#ebdbb2"), commandLine.Normal.Foreground);

    Assert.True(SchemeManager.TryGetScheme("CommandLineError", out var commandLineError));
    Assert.NotNull(commandLineError);
    Assert.Equal(new Color("#262626"), commandLineError.Normal.Foreground);
    Assert.Equal(new Color("#d75f5f"), commandLineError.Normal.Background);
  }

  [Fact]
  public void UserOverrides_ThemeScopedScheme_TakesPrecedenceOverDefaults() {
    ApplyConfigWithDefaults("""
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
    """);

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
    ApplyConfigWithDefaults("""
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
    """);

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
    ApplyConfigWithDefaults("""
    {
      "Schemes": {
        "Accent": {
          "Normal": { "Foreground": "#334455", "Background": "None" }
        }
      }
    }
    """);

    // Overridden scheme should use custom value
    Assert.True(SchemeManager.TryGetScheme("Accent", out var accentScheme));
    Assert.NotNull(accentScheme);
    Assert.Equal(new Color("#334455"), accentScheme.Normal.Foreground);

    // Non-overridden schemes should fall back to SMoC default values from embedded config.json
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
    ApplyConfigWithDefaults("""
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
    """);

    Assert.Equal("NordicTheme", ThemeManager.Theme);
    Assert.True(SchemeManager.TryGetScheme("Accent", out var accentScheme));
    Assert.NotNull(accentScheme);
    Assert.Equal(new Color("#88c0d0"), accentScheme.Normal.Foreground);
    Assert.Equal(new Color("#2e3440"), accentScheme.Normal.Background);
  }

  [Fact]
  public void ThemeChange_AppliesNewThemeSchemes() {
    ApplyConfigWithDefaults("""
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
    """);

    Assert.Equal("ThemeOne", ThemeManager.Theme);
    Assert.Equal(new Color("#111111"), SchemeManager.GetScheme("Accent").Normal.Foreground);
    Assert.Equal(new Color("#ebdbb2"), SchemeManager.GetScheme("ProgressBar").Normal.Foreground);

    // Switch theme to ThemeTwo at runtime
    ThemeManager.Theme = "ThemeTwo";

    Assert.Equal("ThemeTwo", ThemeManager.Theme);
    Assert.Equal(new Color("#222222"), SchemeManager.GetScheme("Accent").Normal.Foreground);
    Assert.True(SchemeManager.TryGetScheme("ProgressBar", out var progressBarScheme));
    Assert.NotNull(progressBarScheme);
    Assert.Equal(new Color("#ebdbb2"), progressBarScheme.Normal.Foreground);
  }
}
