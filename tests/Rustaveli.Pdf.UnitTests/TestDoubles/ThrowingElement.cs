namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// An element that measures normally and then fails while drawing, the way a broken component or an
/// undecodable image would.
/// </summary>
public sealed class ThrowingElement(Exception exception) : Block
{
    public override Fit Measure(Extent availableSpace, PlanContext context) =>
        Fit.FullRender(10, 10);

    public override void Draw(Extent availableSpace, RenderContext context) => throw exception;
}
