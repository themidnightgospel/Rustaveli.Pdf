using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Gives its child no more than the child's own size on the axes it fits, instead of the whole box, so a fill or
/// stroke hugs the content. Across, the content starts where the reading direction starts: at the right, right to
/// left.
/// </summary>
internal sealed class FitToContentBlock : EnclosingBlock
{
    public bool Across { get; set; } = true;

    public bool Down { get; set; } = true;

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (Child is null)
            return;

        Fit plan = Child.Plan(availableSpace, context.Planning);

        if (plan.IsDeferred || plan.IsNothing)
            return;

        Extent size = new Extent(
            Across ? plan.Size.Width : availableSpace.Width,
            Down ? plan.Size.Height : availableSpace.Height);

        float start = Across && context.Planning.ReadingDirection == ReadingDirection.RightToLeft
            ? availableSpace.Width - size.Width
            : 0f;

        context.Surface.MoveOrigin(new Offset(start, 0));
        context.RenderAllotted(Child, size, availableSpace.Height);
        context.Surface.MoveOrigin(new Offset(-start, 0));
    }
}
