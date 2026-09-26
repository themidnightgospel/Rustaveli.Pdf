using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Draws a raster image, scaled according to its <see cref="ImageFitting"/>.
/// </summary>
public sealed class ImageElement : Block
{
    public IImage? Image { get; set; }

    public ImageFitting Fit { get; set; } = ImageFitting.Width;

    public override Fit Measure(Extent availableSpace, PlanContext context)
    {
        if (Image is null)
            return Layout.Fit.FullRender(Extent.Zero);

        Extent size = ResolveSize(availableSpace);

        if (!size.FitsIn(availableSpace))
            return Layout.Fit.Wrap("The available space is too small for the image at its requested fit.");

        return Layout.Fit.FullRender(size);
    }

    public override void Draw(Extent availableSpace, RenderContext context)
    {
        if (Image is null)
            return;

        Extent size = ResolveSize(availableSpace);

        if (!size.FitsIn(availableSpace))
            return;

        context.Canvas.DrawImage(Image, size);
    }

    private Extent ResolveSize(Extent availableSpace)
    {
        // Width divided by height, used to derive layout size from one known dimension. Computed here rather than
        // as a default interface member, which the netstandard2.0 runtime cannot dispatch.
        float ratio = (Image!.PixelHeight == 0) ? 1f : ((float)Image.PixelWidth / (float)Image.PixelHeight);

        Extent fromWidth = new Extent(availableSpace.Width, availableSpace.Width / ratio);
        Extent fromHeight = new Extent(availableSpace.Height * ratio, availableSpace.Height);

        return Fit switch
        {
            ImageFitting.Width => fromWidth,
            ImageFitting.Height => fromHeight,
            ImageFitting.Area => fromWidth.Height <= availableSpace.Height ? fromWidth : fromHeight,
            ImageFitting.Unproportional => availableSpace,
            _ => fromWidth
        };
    }
}
