using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Draws vector artwork, scaled to its frame as an image is, and clipped to the artwork's own bounds.
/// </summary>
internal sealed class ArtworkBlock : Block
{
    public required Artwork Artwork { get; init; }

    public ImageFitting Fit { get; init; } = ImageFitting.FitWidth;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        Extent size = ResolveSize(availableSpace);

        return size.FitsIn(availableSpace)
            ? Layout.Fit.Complete(size)
            : Layout.Fit.Defer("The space available is too small for the artwork as fitted.");
    }

    public override void Render(Extent availableSpace, RenderContext context)
    {
        Extent size = ResolveSize(availableSpace);

        if (!size.FitsIn(availableSpace))
            return;

        Extent own = Artwork.Size;
        context.Surface.Save();
        context.Surface.ClipRectangle(size);
        context.Surface.Scale(size.Width / own.Width, size.Height / own.Height);
        Artwork.Render(context.Surface, context.Measurer);
        context.Surface.Restore();
    }

    private Extent ResolveSize(Extent availableSpace)
    {
        float ratio = Artwork.Size.Width / Artwork.Size.Height;
        Extent fromWidth = new Extent(availableSpace.Width, availableSpace.Width / ratio);
        Extent fromHeight = new Extent(availableSpace.Height * ratio, availableSpace.Height);

        return Fit switch
        {
            ImageFitting.FitHeight => fromHeight,
            ImageFitting.Proportionally => fromWidth.Height <= availableSpace.Height ? fromWidth : fromHeight,
            ImageFitting.Stretch => availableSpace,
            _ => fromWidth,
        };
    }
}
