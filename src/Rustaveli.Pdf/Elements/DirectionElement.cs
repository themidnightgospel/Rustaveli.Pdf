using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Overrides the content direction for everything beneath it.
/// </summary>
/// <remarks>
/// Lets a right-to-left passage sit inside a left-to-right document, or the reverse, without either having to
/// know about the other.
/// </remarks>
public sealed class DirectionElement : EnclosingBlock
{
    public ReadingDirection Direction { get; set; } = ReadingDirection.LeftToRight;

    public override Fit Measure(Extent availableSpace, PlanContext context) =>
        context.WithDirection(Direction, () => base.Measure(availableSpace, context));

    public override void Draw(Extent availableSpace, RenderContext context) =>
        context.Layout.WithDirection(Direction, () => base.Draw(availableSpace, context));
}
