using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Draws its child in a box only as large as the child itself on the axes chosen, rather than in all the room it is
/// given, so a fill or stroke around it hugs the content. It is planned as its child is.
/// </summary>
internal sealed class FitToContentBlock : EnclosingBlock
{
    /// <summary>Whether the box is as wide as the content; true unless set otherwise.</summary>
    public bool Across { get; set; } = true;

    /// <summary>Whether the box is as tall as the content; true unless set otherwise.</summary>
    public bool Down { get; set; } = true;

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (Child is null)
            return;

        Fit plan = Child.Plan(availableSpace, context.Planning);

        if (!plan.PlacesContent)
            return;

        Extent box = new Extent(
            Across ? plan.Size.Width : availableSpace.Width,
            Down ? plan.Size.Height : availableSpace.Height);

        // Narrowed content starts where the reading does, which right to left is the right-hand edge.
        float left = Across && context.Planning.ReadingDirection == ReadingDirection.RightToLeft
            ? availableSpace.Width - box.Width
            : 0f;

        Offset shift = new Offset(left, 0);
        context.Surface.MoveOrigin(shift);
        context.RenderAllotted(Child, box, availableSpace.Height);
        context.Surface.MoveOrigin(shift.Reverse());
    }
}
