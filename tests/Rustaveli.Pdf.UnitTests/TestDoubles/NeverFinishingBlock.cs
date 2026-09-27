namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// An element that always reports content remaining yet never consumes any space.
/// </summary>
/// <remarks>
/// The classic non-terminating layout: every page it is given ends with a promise of more, so pagination would
/// run forever without a limit.
/// </remarks>
internal sealed class NeverFinishingBlock : Block
{
    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        Fit.Partial(Extent.Zero);

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
    }
}
