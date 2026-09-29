namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// An element that measures normally and then fails while drawing, the way a broken component or an
/// undecodable image would — or fails as soon as it is measured, when <paramref name="whileMeasured"/> is set.
/// </summary>
internal sealed class ThrowingBlock(Exception exception, bool whileMeasured = false) : Block
{
    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        whileMeasured ? throw exception : Fit.Complete(10, 10);

    protected override void RenderCore(Extent availableSpace, RenderContext context) => throw exception;
}
