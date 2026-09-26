using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Draws a raster image, scaled according to its <see cref="ImageFit"/>.
/// </summary>
public sealed class ImageElement : Element
{
    public IImage? Image { get; set; }

    public ImageFit Fit { get; set; } = ImageFit.Width;

    public override SpacePlan Measure(Size availableSpace, LayoutContext context)
    {
        if (Image is null)
            return SpacePlan.FullRender(Size.Zero);

        Size size = ResolveSize(availableSpace);

        if (!size.FitsIn(availableSpace))
            return SpacePlan.Wrap("The available space is too small for the image at its requested fit.");

        return SpacePlan.FullRender(size);
    }

    public override void Draw(Size availableSpace, DrawContext context)
    {
        if (Image is null)
            return;

        Size size = ResolveSize(availableSpace);

        if (!size.FitsIn(availableSpace))
            return;

        context.Canvas.DrawImage(Image, size);
    }

    private Size ResolveSize(Size availableSpace)
    {
        // Width divided by height, used to derive layout size from one known dimension. Computed here rather than
        // as a default interface member, which the netstandard2.0 runtime cannot dispatch.
        float ratio = (Image!.PixelHeight == 0) ? 1f : ((float)Image.PixelWidth / (float)Image.PixelHeight);

        Size fromWidth = new Size(availableSpace.Width, availableSpace.Width / ratio);
        Size fromHeight = new Size(availableSpace.Height * ratio, availableSpace.Height);

        return Fit switch
        {
            ImageFit.Width => fromWidth,
            ImageFit.Height => fromHeight,
            ImageFit.Area => fromWidth.Height <= availableSpace.Height ? fromWidth : fromHeight,
            ImageFit.Unproportional => availableSpace,
            _ => fromWidth
        };
    }
}
