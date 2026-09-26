namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// An element that measures normally and then fails while drawing, the way a broken component or an
/// undecodable image would.
/// </summary>
public sealed class ThrowingElement(Exception exception) : Element
{
    public override SpacePlan Measure(Size availableSpace, LayoutContext context) =>
        SpacePlan.FullRender(10, 10);

    public override void Draw(Size availableSpace, DrawContext context) => throw exception;
}
