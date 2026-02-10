using System.Drawing;
using Smoc.Ui.Drawing;
using Terminal.Gui.Drawing;

namespace smoc.Tests.Fakes;

/// <summary>
/// A fake implementation of <see cref="ISixelDriver"/> for use in tests.
/// </summary>
public class FakeSixelDriver : ISixelDriver {
  /// <inheritdoc/>
  public double CellAspectRatio => 2.0;

  /// <inheritdoc/>
  public bool IsSupported => true;

  /// <inheritdoc/>
  public Size? Resolution => null;

  /// <inheritdoc/>
  public string EncodeSixel(Terminal.Gui.Drawing.Color[,] colors) {
    return "";
  }

  /// <inheritdoc/>
  public void EnqueueSixel(SixelToRender sixelToRender) {
    return;
  }

  /// <inheritdoc/>
  public void Initialize() {
    return;
  }
}