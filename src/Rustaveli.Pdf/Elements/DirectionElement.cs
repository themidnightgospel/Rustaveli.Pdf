using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Overrides the content direction for everything beneath it.
/// </summary>
/// <remarks>
/// Lets a right-to-left passage sit inside a left-to-right document, or the reverse, without either having to
/// know about the other.
/// </remarks>
public sealed class DirectionElement : ContainerElement
{
    public ContentDirection Direction { get; set; } = ContentDirection.LeftToRight;

    public override SpacePlan Measure(Size availableSpace, LayoutContext context) =>
        context.WithDirection(Direction, () => base.Measure(availableSpace, context));

    public override void Draw(Size availableSpace, DrawContext context) =>
        context.Layout.WithDirection(Direction, () => base.Draw(availableSpace, context));
}
