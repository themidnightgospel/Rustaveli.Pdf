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

        context.Surface.PaintImage(Image, size);
    }

    private Extent ResolveSize(Extent availableSpace) => throw new NotImplementedException("To be written anew from its specification.");
}
