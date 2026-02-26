using System.Drawing;
using Terminal.Gui.Drawing;
using Color = Terminal.Gui.Drawing.Color;

namespace Smoc.Ui.Drawing;

/// <summary>
/// Provides an abstraction over Sixel graphics rendering.
/// </summary>
public interface ISixelDriver {

  /// <summary>
  /// Gets the aspect ratio (height/width) of terminal character cells in pixels.
  /// </summary>
  double CellAspectRatio { get; }

  /// <summary>
  /// Gets a value indicating whether the terminal supports Sixel graphics.
  /// </summary>
  bool IsSupported { get; }

  /// <summary>
  /// Gets the detected pixel resolution of a terminal character cell, or <c>null</c> if
  /// detection has not yet completed.
  /// </summary>
  Size? Resolution { get; }

  /// <summary>
  /// The maximum number of colors that can be included in a sixel image. Defaults
  /// to 256.
  /// </summary>
  int MaxPaletteColors { get; }

  /// <summary>
  /// Initializes the Sixel driver, triggering asynchronous detection of Sixel support
  /// and terminal cell resolution.
  /// </summary>
  void Initialize();

  /// <summary>
  /// Enqueues a Sixel image for rendering in the terminal. If the driver has not yet
  /// finished initializing, the render request is deferred until initialization completes.
  /// </summary>
  /// <param name="sixelToRender">The Sixel render request to enqueue.</param>
  void EnqueueSixel(SixelToRender sixelToRender);

  /// <summary>
  /// Encodes a two-dimensional array of colors into a Sixel-formatted string.
  /// </summary>
  /// <param name="colors">A 2D array of colors representing the image to encode.</param>
  /// <returns>A Sixel-encoded string representation of the image.</returns>
  string EncodeSixel(Color[,] colors);
}