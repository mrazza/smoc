namespace Smoc.Ui.Drawing;

using System.Collections.Concurrent;
using System.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Color = Terminal.Gui.Drawing.Color;

/// <summary>
/// Standard implementation of <see cref="ISixelDriver"/>.
/// </summary>
public sealed class SixelDriver : ISixelDriver {
  private SixelSupportDetector? _sixelSupportDetector;
  private SixelSupportResult? _sixelSupportResult;
  private readonly IApplication _app;
  private readonly ConcurrentQueue<Action<SixelDriver>> _sixelInitQueue = new();

  /// <inheritdoc/>
  public double CellAspectRatio =>
      _sixelSupportResult?.Resolution.Height / (double?)_sixelSupportResult?.Resolution.Width ?? 2.0;

  /// <inheritdoc/>
  public bool IsSupported => _sixelSupportResult?.IsSupported ?? false;

  /// <inheritdoc/>
  public Size? Resolution => _sixelSupportResult?.Resolution;

  /// <inheritdoc/>
  public int MaxPaletteColors => _sixelSupportResult?.MaxPaletteColors ?? 256;

  /// <summary>
  /// Initializes a new instance of the <see cref="SixelDriver"/> class.
  /// </summary>
  /// <param name="app">The application instance to use when rendering.</param>
  public SixelDriver(IApplication app) {
    _app = app;
  }

  /// <inheritdoc/>
  public void Initialize() {
    EnsureInitialized();
  }

  /// <inheritdoc/>
  public void EnqueueSixel(SixelToRender sixelToRender) {
    if (_sixelSupportResult is null) {
      _sixelInitQueue.Enqueue((driver) => driver.EnqueueSixel(sixelToRender));
      EnsureInitialized();
    } else if (_sixelSupportResult is not null && _sixelSupportResult.IsSupported) {
      if (!_app.Driver!.GetSixels().Contains(sixelToRender)) {
        _app.Driver!.GetSixels().Enqueue(sixelToRender);
      }
    }
  }

  /// <inheritdoc/>
  public string EncodeSixel(Color[,] colors) {
    var encoder = new SixelEncoder();
    encoder.Quantizer.MaxColors = Math.Min(encoder.Quantizer.MaxColors, MaxPaletteColors);
    return encoder.EncodeSixel(colors);
  }

  private void EnsureInitialized() {
    if (_sixelSupportDetector is not null) {
      return;
    }
    _sixelSupportDetector = new SixelSupportDetector(_app.Driver);
    _sixelSupportDetector.Detect((result) => {
      _app.Invoke(() => {
        _sixelSupportResult = result;
        while (_sixelInitQueue.TryDequeue(out var action)) {
          action(this);
        }
      });
    });
  }
}