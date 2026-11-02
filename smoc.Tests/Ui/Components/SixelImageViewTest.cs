using System.Drawing;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Smoc.Ui.Components;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Xunit;
using Rectangle = System.Drawing.Rectangle;

namespace smoc.Tests.Ui.Components;

/// <summary>
/// Unit tests for <see cref="SixelImageView"/>.
/// </summary>
public class SixelImageViewTest {
  private static Image<Rgba32> CreateTestImage(int width = 20, int height = 20) => new(width, height);

  private static Mock<IDriver> CreateMockDriver(bool sixelSupported = false) {
    var mockDriver = new Mock<IDriver>();
    var sixelResult = new SixelSupportResult {
      IsSupported = sixelSupported,
      Resolution = new System.Drawing.Size(10, 20)
    };
    mockDriver.SetupGet(d => d.SixelSupport).Returns(sixelResult);
    return mockDriver;
  }

  private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout, CancellationToken cancellationToken) {
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    cts.CancelAfter(timeout);
    while (!condition()) {
      cts.Token.ThrowIfCancellationRequested();
      await Task.Delay(10, cts.Token);
    }
  }

  /// <summary>
  /// Verifies that setting an image when Driver is null does not throw and leaves Image null.
  /// </summary>
  [Fact]
  public void SetImage_WhenDriverIsNull_DoesNotThrowAndImageRemainsNull() {
    using var image = CreateTestImage();
    using var sut = new SixelImageView();

    sut.SetImage(image);

    Assert.Null(sut.Image);
  }

  /// <summary>
  /// Verifies that setting an image when Sixel is not supported does not throw and leaves Image null.
  /// </summary>
  [Fact]
  public void SetImage_WhenSixelNotSupported_DoesNotThrowAndImageRemainsNull() {
    using var image = CreateTestImage();
    var mockDriver = CreateMockDriver(sixelSupported: false);

    using var sut = new SixelImageView {
      Driver = mockDriver.Object,
      Frame = new Rectangle(0, 0, 10, 5)
    };

    sut.SetImage(image);

    Assert.Null(sut.Image);
  }

  /// <summary>
  /// Verifies that when Sixel support becomes available after setting an image,
  /// SixelImageView reacts to SixelSupportChanged and generates Image data.
  /// </summary>
  [Fact]
  public async Task SixelSupportChanged_WhenSixelBecomesSupported_GeneratesAndSetsImage() {
    using var image = CreateTestImage();
    var mockDriver = CreateMockDriver(sixelSupported: false);

    using var sut = new SixelImageView {
      Driver = mockDriver.Object,
      Frame = new Rectangle(0, 0, 10, 5)
    };

    sut.SetImage(image);
    Assert.Null(sut.Image);

    var supportedResult = new SixelSupportResult {
      IsSupported = true,
      Resolution = new System.Drawing.Size(10, 20)
    };
    mockDriver.SetupGet(d => d.SixelSupport).Returns(supportedResult);
    mockDriver.Raise(d => d.SixelSupportChanged += null, mockDriver.Object, new ValueChangedEventArgs<SixelSupportResult?>(null, supportedResult));

    await WaitForAsync(() => sut.Image != null, TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

    Assert.NotNull(sut.Image);
  }

  /// <summary>
  /// Verifies that when Sixel is already supported and the view is initialized,
  /// the image is rendered automatically.
  /// </summary>
  [Fact]
  public async Task Initialized_WhenSixelAlreadySupported_RendersImage() {
    using var image = CreateTestImage();
    var mockDriver = CreateMockDriver(sixelSupported: true);

    using var sut = new SixelImageView {
      Driver = mockDriver.Object,
      Frame = new Rectangle(0, 0, 10, 5)
    };

    sut.SetImage(image);

    sut.BeginInit();
    sut.EndInit();

    await WaitForAsync(() => sut.Image != null, TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

    Assert.NotNull(sut.Image);
  }

  /// <summary>
  /// Verifies that ClearImage clears both the underlying image and the rendered Image property.
  /// </summary>
  [Fact]
  public async Task ClearImage_WhenImageRendered_ClearsImage() {
    using var image = CreateTestImage();
    var mockDriver = CreateMockDriver(sixelSupported: true);

    using var sut = new SixelImageView {
      Driver = mockDriver.Object,
      Frame = new Rectangle(0, 0, 10, 5)
    };

    sut.SetImage(image);
    await WaitForAsync(() => sut.Image != null, TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);

    Assert.NotNull(sut.Image);

    sut.ClearImage();

    Assert.Null(sut.Image);
  }

  /// <summary>
  /// Verifies that Dispose cleanly unsubscribes from the driver and handles cancellation.
  /// </summary>
  [Fact]
  public void Dispose_UnsubscribesFromDriver() {
    var mockDriver = CreateMockDriver(sixelSupported: false);

    var sut = new SixelImageView {
      Driver = mockDriver.Object,
      Frame = new Rectangle(0, 0, 10, 5)
    };

    sut.BeginInit();
    sut.EndInit();

    sut.Dispose();

    var supportedResult = new SixelSupportResult {
      IsSupported = true,
      Resolution = new System.Drawing.Size(10, 20)
    };
    mockDriver.Raise(d => d.SixelSupportChanged += null, mockDriver.Object, new ValueChangedEventArgs<SixelSupportResult?>(null, supportedResult));
  }
}
