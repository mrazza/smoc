using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Color = Terminal.Gui.Drawing.Color;

namespace Smoc.Ui.Components;

/// <summary>
/// A view that renders images using Sixel graphics sequences.
/// </summary>
public sealed class SixelImageView : View {
  private readonly IMainWindow _mainWindow;
  private Image<Rgba32>? _image;
  private SixelToRender? _sixelToRender;

  /// <summary>
  /// Initializes a new instance of the <see cref="SixelImageView"/> class.
  /// </summary>
  /// <param name="mainWindow">The main window.</param>
  /// <param name="image">The initial image to display.</param>
  public SixelImageView(IMainWindow mainWindow, Image<Rgba32>? image = null) {
    _mainWindow = mainWindow;
    _image = image;
  }

  /// <summary>
  /// Clears the currently displayed image.
  /// </summary>
  public void ClearImage() {
    if (_image is null) {
      return;
    }

    _image = null;
    _sixelToRender = null;
    SetNeedsDraw();
  }

  /// <summary>
  /// Sets a new image to display.
  /// </summary>
  /// <param name="image">The image to display.</param>
  public void SetImage(Image<Rgba32> image) {
    _image = image;
    _sixelToRender = null;
    UpdateSixelData();
    SetNeedsDraw();
  }

  protected override void OnFrameChanged(in System.Drawing.Rectangle frame) {
    base.OnFrameChanged(frame);
    UpdateSixelData();
    SetNeedsDraw();
  }

  protected override bool OnDrawingContent(DrawContext? context) {
    base.OnDrawingContent(context);

    if (_sixelToRender is not null) {
      _mainWindow.SixelDriver.EnqueueSixel(_sixelToRender);
      context?.AddDrawnRectangle(GetRenderableArea());
      return true;
    }

    return false;
  }

  private System.Drawing.Rectangle GetRenderableArea() {
    var frame = FrameToScreen();
    return new(
      frame.X + (Margin?.Thickness.Left ?? 0),
      frame.Y + (Margin?.Thickness.Top ?? 0),
      frame.Width - (Margin?.Thickness.Horizontal ?? 0),
      frame.Height - (Margin?.Thickness.Vertical ?? 0));
  }

  private void UpdateSixelData() {
    if (_image is null || !_mainWindow.SixelDriver.IsSupported) {
      return;
    }

    var boundsRect = GetRenderableArea();
    var resizedImage = _image.Clone(
        i => i.Resize(boundsRect.Width * _mainWindow.SixelDriver.Resolution!.Value.Width, boundsRect.Height * _mainWindow.SixelDriver.Resolution!.Value.Height));
    _sixelToRender = new SixelToRender() {
      SixelData = _mainWindow.SixelDriver.EncodeSixel(ConvertToColorArray(resizedImage)),
      ScreenPosition = new System.Drawing.Point(boundsRect.X, boundsRect.Y)
    };
  }

  private static Color[,] ConvertToColorArray(Image<Rgba32> image) {
    int width = image.Width;
    int height = image.Height;
    var colors = new Color[width, height];

    for (var x = 0; x < width; x++) {
      for (var y = 0; y < height; y++) {
        var pixel = image[x, y];
        colors[x, y] = new(pixel.R, pixel.G, pixel.B);
      }
    }

    return colors;
  }
}
