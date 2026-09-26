namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// An element that always reports content remaining yet never consumes any space.
/// </summary>
/// <remarks>
/// The classic non-terminating layout: every page it is given ends with a promise of more, so pagination would
/// run forever without a limit.
/// </remarks>
public sealed class NeverFinishingBlock : Block
{
    public override Fit Plan(Extent availableSpace, PlanContext context) =>
        Fit.Partial(Extent.Zero);

    public override void Render(Extent availableSpace, RenderContext context)
    {
    }
}
