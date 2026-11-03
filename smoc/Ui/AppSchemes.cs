using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;

namespace Smoc.Ui;

/// <summary>
/// Defines and registers application-specific color schemes.
/// </summary>
public static class AppSchemes {
  private static volatile bool _isInitialized = false;
  private static readonly object _lock = new();

  /// <summary>
  /// Registers the default SMoC color schemes with <see cref="SchemeManager"/>, preserving any existing
  /// configuration overrides, and subscribes to <see cref="ThemeManager.ThemeChanged"/> to backfill
  /// defaults on theme changes.
  /// </summary>
  public static void RegisterDefaultSchemes() {
    lock (_lock) {
      if (!_isInitialized) {
        ThemeManager.ThemeChanged += OnThemeChanged;
        _isInitialized = true;
      }

      ApplyDefaultSchemes();
    }
  }

  private static void OnThemeChanged(object? sender, EventArgs<string> args) {
    lock (_lock) {
      ApplyDefaultSchemes();
    }
  }

  /// <summary>
  /// Populates SMoC default schemes into <see cref="SchemeManager.Schemes"/> for any schemes not defined
  /// or customized by the active theme.
  /// </summary>
  public static void ApplyDefaultSchemes() {
    foreach (var (name, schemeFactory) in _defaultSchemeFactories) {
      if (ShouldRegisterDefaultScheme(name)) {
        SchemeManager.Schemes[name] = schemeFactory();
      }
    }
  }

  private static bool ShouldRegisterDefaultScheme(string name) {
    if (!SchemeManager.TryGetScheme(name, out var existing) || existing is null) {
      return true;
    }

    // Terminal.Gui includes built-in hardcoded Accent and Menu schemes. If they match the built-in
    // attributes, they have not been overridden by user/theme configuration.
    if (name == "Accent" &&
        existing.Normal.Foreground == Color.None &&
        existing.Normal.Background == Color.None &&
        existing.Focus.Foreground == Color.Black &&
        existing.Focus.Background == Color.White) {
      return true;
    }

    if (name == "Menu" &&
        existing.Normal.Foreground == new Color("Charcoal") &&
        existing.Normal.Background == new Color("LightBlue")) {
      return true;
    }

    return false;
  }

  /// <summary>
  /// Resets registered schemes and unregisters event handlers (for unit tests).
  /// </summary>
  internal static void ResetForTesting() {
    lock (_lock) {
      if (_isInitialized) {
        ThemeManager.ThemeChanged -= OnThemeChanged;
        _isInitialized = false;
      }

      foreach (var name in _defaultSchemeFactories.Keys) {
        SchemeManager.Schemes.Remove(name);
      }
    }
  }

  private static readonly Dictionary<string, Func<Scheme>> _defaultSchemeFactories = new() {
    ["Accent"] = () => new Scheme {
      Normal = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), Color.None),
      Focus = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#639494")),
      Active = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#394e4e"))
    },

    ["ProgressBar"] = () => new Scheme {
      Normal = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#394e4e"))
    },

    ["TableCurrentTrack"] = () => new Scheme {
      Normal = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#394e4e"), TextStyle.Bold),
      Focus = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#639494"), TextStyle.Bold),
      Active = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#394e4e"), TextStyle.Bold)
    },

    ["TableHeaders"] = () => new Scheme {
      Normal = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), Color.None),
      Focus = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), Color.None),
      Active = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), Color.None)
    },

    ["TableNormalTracks"] = () => new Scheme {
      Normal = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), Color.None),
      Focus = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#639494")),
      Active = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#394e4e"))
    },

    ["Menu"] = () => new Scheme {
      Normal = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#3a3a3a")),
      Focus = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#639494")),
      Active = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#394e4e"))
    },

    ["StatusBar"] = () => new Scheme {
      Normal = new Terminal.Gui.Drawing.Attribute(new Color("#262626"), new Color("#949494"), TextStyle.Bold)
    },

    ["StatusBar_State"] = () => new Scheme {
      Normal = new Terminal.Gui.Drawing.Attribute(new Color("#949494"), new Color("#3a3a3a"))
    },

    ["CommandLine"] = () => new Scheme {
      Normal = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), Color.None),
      Editable = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), Color.None)
    },

    ["CommandLineError"] = () => new Scheme {
      Normal = new Terminal.Gui.Drawing.Attribute(new Color("#262626"), new Color("#d75f5f"), TextStyle.Bold)
    }
  };
}
