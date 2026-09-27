using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// An image generated for the box it fills, at the resolution images are generated at: a chart drawn by another
/// library, say, sharp at exactly the size it is shown.
/// </summary>
/// <remarks>
/// The image takes all the room it is given. It is not generated while pages are only being counted, when nothing
/// is drawn.
/// </remarks>
internal sealed class GeneratedImageBlock : Block
{
    public required Func<ImageRequest, byte[]?> Generate { get; init; }

    protected override Fit PlanCore(Extent availableSpace, PlanContext context) => Fit.Complete(availableSpace);

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (context.Surface is CountingPageSink || availableSpace.Width <= 0 || availableSpace.Height <= 0)
            return;

        float resolution = context.Planning.Resolution;
        ImageRequest request = new ImageRequest(
            availableSpace,
            Math.Max(1, (int)Math.Ceiling(availableSpace.Width / 72 * resolution)),
            Math.Max(1, (int)Math.Ceiling(availableSpace.Height / 72 * resolution)),
            resolution);

        if (Generate(request) is { Length: > 0 } image)
            context.Surface.DrawImage(RasterImage.FromBytes(image), availableSpace);
    }
}
