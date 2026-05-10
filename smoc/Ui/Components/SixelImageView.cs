using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Terminal.Gui.Views;
using Color = Terminal.Gui.Drawing.Color;

namespace Smoc.Ui.Components;

/// <summary>
/// A view that renders images using Sixel graphics sequences.
/// </summary>
public sealed class SixelImageView : ImageView {
  private Image<Rgba32>? _image;

  /// <summary>
  /// Initializes a new instance of the <see cref="SixelImageView"/> class.
  /// </summary>
  /// <param name="image">The initial image to display.</param>
  public SixelImageView(Image<Rgba32>? image = null) {
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
    SetNeedsDraw();
  }

  /// <summary>
  /// Sets a new image to display.
  /// </summary>
  /// <param name="image">The image to display.</param>
  public void SetImage(Image<Rgba32> image) {
    _image = image;
    UpdateSixelData();
    SetNeedsDraw();
  }

  protected override void OnFrameChanged(in System.Drawing.Rectangle frame) {
    UpdateSixelData();
    SetNeedsDraw();
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
    if (_image is null || App?.Driver?.SixelSupport is not { IsSupported: true }) {
      return;
    }

    var resolution = App!.Driver!.SixelSupport!.Resolution;
    var boundsRect = GetRenderableArea();
    var resizedImage = _image.Clone(
        i => i.Resize(boundsRect.Width * resolution.Width, boundsRect.Height * resolution.Height));
    Image = ConvertToColorArray(resizedImage);
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
