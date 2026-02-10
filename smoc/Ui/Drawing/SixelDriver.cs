namespace Smoc.Ui.Drawing;

using System.Collections.Concurrent;
using System.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Color = Terminal.Gui.Drawing.Color;

public sealed class SixelDriver : ISixelDriver {
  private SixelSupportDetector? _sixelSupportDetector;
  private SixelSupportResult? _sixelSupportResult;
  private readonly SixelEncoder _encoder;
  private readonly IApplication _app;
  private readonly ConcurrentQueue<Action<SixelDriver>> _sixelInitQueue = new();

  /// <summary>
  /// Gets the aspect ratio (height/width) of terminal character cells in pixels.
  /// Returns 2.0 as a default if not yet detected.
  /// </summary>
  public double CellAspectRatio =>
      _sixelSupportResult?.Resolution.Height / (double?)_sixelSupportResult?.Resolution.Width ?? 2.0;

  public bool IsSupported => _sixelSupportResult?.IsSupported ?? false;

  public Size? Resolution => _sixelSupportResult?.Resolution;

  public SixelDriver(IApplication app) {
    _encoder = new SixelEncoder();
    _app = app;
  }

  public void Initialize() {
    EnsureInitialized();
  }

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

  public string EncodeSixel(Color[,] colors) {
    return _encoder.EncodeSixel(colors);
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