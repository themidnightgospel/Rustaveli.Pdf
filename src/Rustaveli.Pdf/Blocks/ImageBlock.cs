using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Draws a raster image, scaled according to its <see cref="ImageFitting"/>.
/// </summary>
internal sealed class ImageBlock : Block
{
    public IImage? Image { get; set; }

    public ImageFitting Fit { get; set; } = ImageFitting.FitWidth;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        if (Image is null)
            return Layout.Fit.Complete(Extent.Zero);

        Extent size = ResolveSize(availableSpace);

        if (!size.FitsIn(availableSpace))
            return Layout.Fit.Defer("The space available is too small for the image as fitted.");

        return Layout.Fit.Complete(size);
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (Image is null)
            return;

        Extent size = ResolveSize(availableSpace);

        if (!size.FitsIn(availableSpace))
            return;

        context.Surface.DrawImage(Image, size);
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
            ImageFitting.FitWidth => fromWidth,
            ImageFitting.FitHeight => fromHeight,
            ImageFitting.Proportionally => fromWidth.Height <= availableSpace.Height ? fromWidth : fromHeight,
            ImageFitting.Stretch => availableSpace,
            _ => fromWidth
        };
    }
}
