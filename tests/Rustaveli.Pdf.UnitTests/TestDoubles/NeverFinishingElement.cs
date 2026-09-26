namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// An element that always reports content remaining yet never consumes any space.
/// </summary>
/// <remarks>
/// The classic non-terminating layout: every page it is given ends with a promise of more, so pagination would
/// run forever without a limit.
/// </remarks>
public sealed class NeverFinishingElement : Element
{
    public override SpacePlan Measure(Size availableSpace, LayoutContext context) =>
        SpacePlan.PartialRender(Size.Zero);

    public override void Draw(Size availableSpace, DrawContext context)
    {
    }
}
