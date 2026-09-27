namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// An element that measures normally and then fails while drawing, the way a broken component or an
/// undecodable image would.
/// </summary>
internal sealed class ThrowingBlock(Exception exception) : Block
{
    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        Fit.Complete(10, 10);

    public override void Render(Extent availableSpace, RenderContext context) => throw exception;
}
