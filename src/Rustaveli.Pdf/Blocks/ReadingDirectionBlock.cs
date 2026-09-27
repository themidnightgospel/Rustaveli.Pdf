using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Overrides the content direction for everything beneath it.
/// </summary>
/// <remarks>
/// Lets a right-to-left passage sit inside a left-to-right document, or the reverse, without either having to
/// know about the other.
/// </remarks>
internal sealed class ReadingDirectionBlock : EnclosingBlock
{
    public ReadingDirection ReadingDirection { get; set; } = ReadingDirection.LeftToRight;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        context.WithReadingDirection(ReadingDirection, () => base.PlanCore(availableSpace, context));

    protected override void RenderCore(Extent availableSpace, RenderContext context) =>
        context.Planning.WithReadingDirection(ReadingDirection, () => base.RenderCore(availableSpace, context));
}
