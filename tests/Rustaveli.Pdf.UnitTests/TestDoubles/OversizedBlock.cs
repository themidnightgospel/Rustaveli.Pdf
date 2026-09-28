namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// An element that reports a fixed size however little space it is offered.
/// </summary>
/// <remarks>
/// Breaks the measuring contract on purpose. Built-in elements clamp or wrap, but <see cref="Block"/> is a
/// public extension point, and the engine has guards for a custom element that promises more than it was given.
/// </remarks>
internal sealed class OversizedBlock(float width, float height) : Block
{
    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        Fit.Complete(width, height);

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
    }
}
