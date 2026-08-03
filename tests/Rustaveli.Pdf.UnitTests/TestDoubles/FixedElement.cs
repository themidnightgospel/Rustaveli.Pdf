using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// An element of a fixed intrinsic size that wraps when it does not fit.
/// </summary>
/// <remarks>
/// Layout behaviour is far easier to assert against a shape of known size than against real content, whose
/// dimensions depend on font metrics and wrapping.
/// </remarks>
public sealed class FixedElement(Size size, Color? color = null) : Element
{
    public FixedElement(float width, float height) : this(new Size(width, height))
    {
    }

    public FixedElement(float width, float height, Color color) : this(new Size(width, height), color)
    {
    }

    public Color Color { get; } = color ?? Colors.Black;

    public override SpacePlan Measure(Size availableSpace, LayoutContext context) =>
        size.FitsIn(availableSpace)
            ? SpacePlan.FullRender(size)
            : SpacePlan.Wrap($"The element requires {size} but only {availableSpace} is available.");

    public override void Draw(Size availableSpace, DrawContext context)
    {
        if (size.FitsIn(availableSpace))
            context.Canvas.DrawRectangle(Position.Zero, size, Color);
    }
}
