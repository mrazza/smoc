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
  private SixelSupportDetector? _sixelSupportDetector;
  private SixelSupportResult? _sixelSupportResult;
  private Image<Rgba32>? _image;
  private SixelToRender? _sixelToRender;
  private readonly SixelEncoder _encoder;

  /// <summary>
  /// Initializes a new instance of the <see cref="SixelImageView"/> class.
  /// </summary>
  /// <param name="image">The initial image to display.</param>
  public SixelImageView(Image<Rgba32>? image = null) {
    _image = image;
    _encoder = new SixelEncoder();
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
    App!.Driver!.GetSixels().Clear();
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

    if (_sixelSupportResult is not null && _sixelSupportResult.IsSupported && _sixelToRender is not null) {
      if (!App!.Driver!.GetSixels().Contains(_sixelToRender)) {
        App!.Driver!.GetSixels().Clear();
        App!.Driver!.GetSixels().Enqueue(_sixelToRender);
      }

      context?.AddDrawnRectangle(RenderableArea);

      return true;
    } else {
      if (_sixelSupportDetector is null) {
        // We delay initialization of sixel support detector until it's needed and we
        // have confidence the driver is accurate.
        _sixelSupportDetector = new SixelSupportDetector(App!.Driver);
        _sixelSupportDetector.Detect((result) => {
          App!.Invoke(() => {
            _sixelSupportResult = result;
            UpdateSixelData();
            SetNeedsDraw();
          });
        });
      }
    }

    return false;
  }

  private System.Drawing.Rectangle RenderableArea => new(
      Frame.X + (Margin?.Thickness.Left ?? 0),
      Frame.Y + (Margin?.Thickness.Top ?? 0),
      Frame.Width - (Margin?.Thickness.Horizontal ?? 0),
      Frame.Height - (Margin?.Thickness.Vertical ?? 0));

  private void UpdateSixelData() {
    if (_image is null || _sixelSupportResult is null) {
      return;
    }

    var resizedImage = _image.Clone(
        i => i.Resize(RenderableArea.Width * _sixelSupportResult!.Resolution.Width, RenderableArea.Height * _sixelSupportResult!.Resolution.Height));
    _sixelToRender = new SixelToRender() {
      SixelData = _encoder.EncodeSixel(ConvertToColorArray(resizedImage)),
      ScreenPosition = new System.Drawing.Point(RenderableArea.X, RenderableArea.Y)
    };
  }

  private static Color[,] ConvertToColorArray(Image<Rgba32> image) {
    int width = image.Width;
    int height = image.Height;
    Color[,] colors = new Color[width, height];

    for (var x = 0; x < width; x++) {
      for (var y = 0; y < height; y++) {
        Rgba32 pixel = image[x, y];
        colors[x, y] = new(pixel.R, pixel.G, pixel.B);
      }
    }

    return colors;
  }
}
