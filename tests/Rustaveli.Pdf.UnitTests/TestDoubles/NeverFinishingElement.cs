namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// An element that always reports content remaining yet never consumes any space.
/// </summary>
/// <remarks>
/// The classic non-terminating layout: every page it is given ends with a promise of more, so pagination would
/// run forever without a limit.
/// </remarks>
public sealed class NeverFinishingElement : Block
{
    public override Fit Measure(Extent availableSpace, PlanContext context) =>
        Fit.PartialRender(Extent.Zero);

    public override void Draw(Extent availableSpace, RenderContext context)
    {
    }
}
