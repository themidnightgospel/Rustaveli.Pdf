namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// An element that reports a fixed size however little space it is offered.
/// </summary>
/// <remarks>
/// Breaks the measuring contract on purpose. Built-in elements clamp or wrap, but <see cref="Block"/> is a
/// public extension point, and the engine has guards for a custom element that promises more than it was given.
/// </remarks>
public sealed class OversizedElement(float width, float height) : Block
{
    public override Fit Measure(Extent availableSpace, PlanContext context) =>
        Fit.FullRender(width, height);

    public override void Draw(Extent availableSpace, RenderContext context)
    {
    }
}
