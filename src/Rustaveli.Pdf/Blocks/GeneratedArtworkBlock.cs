using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Artwork generated for the box it fills, such as an SVG written for exactly that size, stretched to fill it.
/// </summary>
/// <remarks>
/// The artwork takes all the room it is given. It is not generated while pages are only being counted.
/// </remarks>
internal sealed class GeneratedArtworkBlock : Block
{
    public required Func<Extent, Artwork?> Generate { get; init; }

    public override Fit Plan(Extent availableSpace, PlanContext context) => Fit.Complete(availableSpace);

    public override void Render(Extent availableSpace, RenderContext context)
    {
        if (context.Surface is CountingPageSink || availableSpace.Width <= 0 || availableSpace.Height <= 0)
            return;

        if (Generate(availableSpace) is { } artwork)
            new ArtworkBlock { Artwork = artwork, Fit = ImageFitting.Stretch }.Render(availableSpace, context);
    }
}
