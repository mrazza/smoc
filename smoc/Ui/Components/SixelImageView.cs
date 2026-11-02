using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Color = Terminal.Gui.Drawing.Color;

namespace Smoc.Ui.Components;

/// <summary>
/// A view that renders images using Sixel graphics sequences.
/// </summary>
public sealed class SixelImageView : ImageView {
  private Image<Rgba32>? _image;
  private CancellationTokenSource? _cancellationTokenSource;
  private IDriver? _subscribedDriver;
  private IDriver? _driverOverride;

  /// <summary>
  /// Gets or sets the driver used for Sixel detection. Defaults to <see cref="View.App"/>'s driver.
  /// </summary>
  /// <remarks>
  /// Overriding this property is primarily intended for unit testing, allowing mock drivers to be injected
  /// without requiring a fully initialized <see cref="IApplication"/> runtime environment.
  /// </remarks>
  public IDriver? Driver {
    get => _driverOverride ?? App?.Driver;
    set => _driverOverride = value;
  }


  /// <summary>
  /// Initializes a new instance of the <see cref="SixelImageView"/> class.
  /// </summary>
  /// <param name="image">The initial image to display.</param>
  public SixelImageView(Image<Rgba32>? image = null) {
    _image = image;
    Initialized += OnInitialized;
  }

  /// <summary>
  /// Clears the currently displayed image.
  /// </summary>
  public void ClearImage() {
    _cancellationTokenSource?.Cancel();
    _cancellationTokenSource?.Dispose();
    _cancellationTokenSource = null;

    if (_image is null) {
      return;
    }

    _image = null;
    Image = null;
    SetNeedsDraw();
  }

  /// <summary>
  /// Sets a new image to display.
  /// </summary>
  /// <param name="image">The image to display.</param>
  public void SetImage(Image<Rgba32> image) {
    _image = image;
    UpdateSixelData();
  }

  /// <inheritdoc />
  protected override void OnFrameChanged(in System.Drawing.Rectangle frame) {
    base.OnFrameChanged(frame);
    EnsureDriverSubscribed();
    UpdateSixelData();
  }

  /// <inheritdoc />
  protected override void OnSuperViewChanged(ValueChangedEventArgs<View?> args) {
    base.OnSuperViewChanged(args);
    EnsureDriverSubscribed();
    if (_image is not null && Image is null && Driver?.SixelSupport is { IsSupported: true }) {
      UpdateSixelData();
    }
  }

  /// <inheritdoc />
  protected override bool OnDrawingContent(DrawContext? context) {
    EnsureDriverSubscribed();
    if (_image is not null && Image is null && Driver?.SixelSupport is { IsSupported: true }) {
      UpdateSixelData();
    }

    return base.OnDrawingContent(context);
  }

  private void OnInitialized(object? sender, EventArgs e) {
    EnsureDriverSubscribed();
    if (_image is not null && Image is null && Driver?.SixelSupport is { IsSupported: true }) {
      UpdateSixelData();
    }
  }

  private void EnsureDriverSubscribed() {
    var driver = Driver;
    if (driver is not null && !ReferenceEquals(_subscribedDriver, driver)) {
      if (_subscribedDriver is not null) {
        _subscribedDriver.SixelSupportChanged -= OnSixelSupportChanged;
      }

      _subscribedDriver = driver;
      _subscribedDriver.SixelSupportChanged += OnSixelSupportChanged;
    }
  }

  private void OnSixelSupportChanged(object? sender, ValueChangedEventArgs<SixelSupportResult?> e) {
    if (e.NewValue is { IsSupported: true } && _image is not null) {
      SetNeedsLayout();
      UpdateSixelData();
    }
  }

  private void UpdateSixelData() {
    if (_image is null) {
      return;
    }

    EnsureDriverSubscribed();

    if (Driver?.SixelSupport is not { IsSupported: true }) {
      return;
    }

    if (Viewport.Width <= 0 || Viewport.Height <= 0) {
      return;
    }

    CancellationToken token;
    _cancellationTokenSource?.Cancel();
    _cancellationTokenSource?.Dispose();
    _cancellationTokenSource = new CancellationTokenSource();
    token = _cancellationTokenSource.Token;

    Task.Run(() => {
      Color[,] data;
      try {
        data = GenerateSixelData();
      } catch (Exception ex) {
        Logging.Warning($"Failed to render album art: {ex.Message}");
        return;
      }

      App!.Invoke(() => {
        if (token.IsCancellationRequested) {
          return;
        }

        Image = data;
        SetNeedsDraw();
      });
    }, token);
  }

  private Color[,] GenerateSixelData() {
    var support = Driver?.SixelSupport;
    if (_image is null || support is not { IsSupported: true }) {
      throw new InvalidOperationException("Sixel not supported.");
    }

    if (Viewport.Width <= 0 || Viewport.Height <= 0) {
      throw new InvalidOperationException("Viewport must have positive dimensions to generate Sixel data.");
    }

    int pixelsPerCellX = support.Resolution.Width;
    int pixelsPerCellY = support.Resolution.Height;
    int targetWidthInPixels = Viewport.Width * pixelsPerCellX;
    int targetHeightInPixels = Viewport.Height * pixelsPerCellY;

    if (targetWidthInPixels <= 0 || targetHeightInPixels <= 0) {
      throw new InvalidOperationException("Target pixel dimensions must be positive to generate Sixel data.");
    }

    double widthScale = (double)targetWidthInPixels / _image.Width;
    double heightScale = (double)targetHeightInPixels / _image.Height;
    double scale = Math.Min(widthScale, heightScale);

    int newWidth = Math.Max(1, (int)(_image.Width * scale));
    int newHeight = Math.Max(1, (int)(_image.Height * scale));

    using var resizedImage = _image.Clone(i => i.Resize(newWidth, newHeight));
    return ConvertToColorArray(resizedImage);
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

  /// <inheritdoc />
  protected override void Dispose(bool disposing) {
    if (disposing) {
      if (_subscribedDriver is not null) {
        _subscribedDriver.SixelSupportChanged -= OnSixelSupportChanged;
        _subscribedDriver = null;
      }

      _cancellationTokenSource?.Cancel();
      _cancellationTokenSource?.Dispose();
      _cancellationTokenSource = null;
    }

    base.Dispose(disposing);
  }
}
