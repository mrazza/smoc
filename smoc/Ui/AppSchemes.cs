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
  /// Registers the default SMoC color schemes with <see cref="SchemeManager"/>.
  /// </summary>
  public static void RegisterDefaultSchemes() {
    if (_isInitialized) return;
    lock (_lock) {
      if (_isInitialized) return;

      SchemeManager.AddScheme("Accent", new Scheme {
        Normal = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), Color.None),
        Focus = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#639494")),
        Active = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#394e4e"))
      });

      SchemeManager.AddScheme("ProgressBar", new Scheme {
        Normal = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#394e4e"))
      });

      SchemeManager.AddScheme("TableCurrentTrack", new Scheme {
        Normal = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#394e4e"), TextStyle.Bold),
        Focus = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#639494"), TextStyle.Bold),
        Active = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#394e4e"), TextStyle.Bold)
      });

      SchemeManager.AddScheme("TableHeaders", new Scheme {
        Normal = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), Color.None),
        Focus = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), Color.None),
        Active = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), Color.None)
      });

      SchemeManager.AddScheme("TableNormalTracks", new Scheme {
        Normal = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), Color.None),
        Focus = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#639494")),
        Active = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#394e4e"))
      });

      SchemeManager.AddScheme("Menu", new Scheme {
        Normal = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#3a3a3a")),
        Focus = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#639494")),
        Active = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), new Color("#394e4e"))
      });

      SchemeManager.AddScheme("StatusBar", new Scheme {
        Normal = new Terminal.Gui.Drawing.Attribute(new Color("#262626"), new Color("#949494"), TextStyle.Bold)
      });

      SchemeManager.AddScheme("StatusBar_State", new Scheme {
        Normal = new Terminal.Gui.Drawing.Attribute(new Color("#949494"), new Color("#3a3a3a"))
      });

      SchemeManager.AddScheme("CommandLine", new Scheme {
        Normal = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), Color.None),
        Editable = new Terminal.Gui.Drawing.Attribute(new Color("#ebdbb2"), Color.None)
      });

      SchemeManager.AddScheme("CommandLineError", new Scheme {
        Normal = new Terminal.Gui.Drawing.Attribute(new Color("#262626"), new Color("#d75f5f"), TextStyle.Bold)
      });

      _isInitialized = true;
    }
  }
}
