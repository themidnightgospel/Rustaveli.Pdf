using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Places its child within the room it is drawn in: against one edge or the other, or halfway between, on either axis.
/// It takes only the room its child takes, so content sized by what it holds stays that way.
/// </summary>
internal sealed class PlacementBlock : EnclosingBlock
{
    /// <summary>Where across the room the child goes; against the left when not set.</summary>
    public HorizontalPlacement? Horizontal { get; set; }

    /// <summary>Where down the room the child goes; against the top when not set.</summary>
    public VerticalPlacement? Vertical { get; set; }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (Child is null)
            return;

        Fit plan = Child.Plan(availableSpace, context.Planning);

        if (!plan.PlacesContent)
            return;

        float slackAcross = availableSpace.Width - plan.Size.Width;
        float slackDown = availableSpace.Height - plan.Size.Height;

        Offset corner = new Offset(
            Horizontal switch
            {
                HorizontalPlacement.Center => slackAcross / 2,
                HorizontalPlacement.Right => slackAcross,
                _ => 0f,
            },
            Vertical switch
            {
                VerticalPlacement.Middle => slackDown / 2,
                VerticalPlacement.Bottom => slackDown,
                _ => 0f,
            });

        // The child is given a box of its own size, not the whole room: content that places itself within its box, as
        // right-aligned text does, would otherwise be moved twice.
        context.Surface.MoveOrigin(corner);
        context.RenderAllotted(Child, plan.Size, availableSpace.Height);
        context.Surface.MoveOrigin(corner.Reverse());
    }
}
