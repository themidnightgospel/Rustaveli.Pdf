namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// An element of a fixed intrinsic size that wraps when it does not fit.
/// </summary>
/// <remarks>
/// Layout behaviour is far easier to assert against a shape of known size than against real content, whose
/// dimensions depend on font metrics and wrapping.
/// </remarks>
public sealed class FixedElement(Extent size, Ink? color = null) : Block
{
    public FixedElement(float width, float height) : this(new Extent(width, height))
    {
    }

    public FixedElement(float width, float height, Ink color) : this(new Extent(width, height), color)
    {
    }

    public Ink Color { get; } = color ?? TestInks.Black;

    public override Fit Measure(Extent availableSpace, PlanContext context) =>
        size.FitsIn(availableSpace)
            ? Fit.FullRender(size)
            : Fit.Wrap($"The element requires {size} but only {availableSpace} is available.");

    public override void Draw(Extent availableSpace, RenderContext context)
    {
        if (size.FitsIn(availableSpace))
            context.Canvas.DrawRectangle(Offset.Zero, size, Color);
    }
}
