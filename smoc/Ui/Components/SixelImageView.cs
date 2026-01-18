using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Color = Terminal.Gui.Drawing.Color;

namespace Smoc.Ui.Components;

public sealed class SixelImageView : View
{
    private SixelSupportDetector? sixelSupportDetector;
    private SixelSupportResult? sixelSupportResult;
    private Image<Rgba32>? image;
    private SixelToRender? sixelToRender;
    private readonly SixelEncoder encoder;

    public SixelImageView(Image<Rgba32>? image = null)
    {
        this.image = image;
        encoder = new SixelEncoder();
    }

    public void ClearImage()
    {
        if (image is null)
        {
            return;
        }

        image = null;
        sixelToRender = null;
        App!.Driver!.GetSixels().Clear();
        SetNeedsDraw();
    }

    public void SetImage(Image<Rgba32> image)
    {
        this.image = image;
        sixelToRender = null;
        UpdateSixelData();
        SetNeedsDraw();
    }

    protected override void OnFrameChanged(in System.Drawing.Rectangle frame)
    {
        base.OnFrameChanged(frame);
        UpdateSixelData();
        SetNeedsDraw();
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        base.OnDrawingContent(context);

        if (sixelSupportResult is not null && sixelSupportResult.IsSupported && sixelToRender is not null)
        {
            if (!App!.Driver!.GetSixels().Contains(sixelToRender))
            {
                App!.Driver!.GetSixels().Clear();
                App!.Driver!.GetSixels().Enqueue(sixelToRender);
            }

            context?.AddDrawnRectangle(RenderableArea);

            return true;
        }
        else
        {
            if (sixelSupportDetector is null)
            {
                // We delay initialization of sixel support detector until it's needed and we
                // have confidence the driver is accurate.
                sixelSupportDetector = new SixelSupportDetector(App!.Driver);
                sixelSupportDetector.Detect((result) =>
                {
                    App!.Invoke(() =>
                    {
                        sixelSupportResult = result;
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

    private void UpdateSixelData()
    {
        if (image is null || sixelSupportResult is null)
        {
            return;
        }

        var resizedImage = image.Clone(
            i => i.Resize(RenderableArea.Width * sixelSupportResult!.Resolution.Width, RenderableArea.Height * sixelSupportResult!.Resolution.Height));
        sixelToRender = new SixelToRender()
        {
            SixelData = encoder.EncodeSixel(ConvertToColorArray(resizedImage)),
            ScreenPosition = new System.Drawing.Point(RenderableArea.X, RenderableArea.Y)
        };
    }

    private static Color[,] ConvertToColorArray(Image<Rgba32> image)
    {
        int width = image.Width;
        int height = image.Height;
        Color[,] colors = new Color[width, height];

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                Rgba32 pixel = image[x, y];
                colors[x, y] = new(pixel.R, pixel.G, pixel.B);
            }
        }

        return colors;
    }
}