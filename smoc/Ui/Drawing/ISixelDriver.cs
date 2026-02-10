using System.Drawing;
using Terminal.Gui.Drawing;
using Color = Terminal.Gui.Drawing.Color;

namespace Smoc.Ui.Drawing;

public interface ISixelDriver {

  double CellAspectRatio { get; }
  bool IsSupported { get; }
  Size? Resolution { get; }
  void Initialize();
  void EnqueueSixel(SixelToRender sixelToRender);
  string EncodeSixel(Color[,] colors);
}