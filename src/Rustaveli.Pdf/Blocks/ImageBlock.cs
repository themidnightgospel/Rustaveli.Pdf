using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Draws a raster image, scaled according to its <see cref="ImageFitting"/>.
/// </summary>
internal sealed class ImageBlock : Block
{
    public required IImage Image { get; init; }

    public ImageFitting Fit { get; set; } = ImageFitting.FitWidth;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        Extent size = ResolveSize(availableSpace);

        if (!size.FitsIn(availableSpace))
            return Layout.Fit.Defer("The space available is too small for the image as fitted.");

        return Layout.Fit.Complete(size);
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        Extent size = ResolveSize(availableSpace);

        if (!size.FitsIn(availableSpace))
            return;

        context.Surface.PaintImage(Image, size);
    }

    /// <summary>The box the image is drawn in; an image with no height in pixels is taken to be square.</summary>
    private Extent ResolveSize(Extent availableSpace)
    {
        float aspect = Image.PixelHeight == 0 ? 1f : (float)Image.PixelWidth / Image.PixelHeight;
        return PictureBox.Of(Fit, aspect, availableSpace);
    }
}
