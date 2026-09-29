using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Positions its child within the space it is drawn in: measured as its child is, and placed against an edge or
/// centred in whatever room its parent grants — a stack's width, a row's height, a frame of fixed size.
/// </summary>
/// <remarks>
/// Measuring as the child does keeps natural sizing natural: a placed frame in a column sized to its content takes
/// its content's width, not the page's. Where the parent grants no more room than the child needs, placement moves
/// nothing.
/// </remarks>
internal sealed class PlacementBlock : EnclosingBlock
{
    public HorizontalPlacement? Horizontal { get; set; }

    public VerticalPlacement? Vertical { get; set; }

    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        Child?.Plan(availableSpace, context) ?? Fit.Complete(Extent.Zero);

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (Child is null)
            return;

        Fit childPlan = Child.Plan(availableSpace, context.Planning);

        if (childPlan.IsDeferred || childPlan.IsNothing)
            return;

        Offset offset = new Offset(
            HorizontalOffset(availableSpace.Width, childPlan.Size.Width),
            VerticalOffset(availableSpace.Height, childPlan.Size.Height));

        context.Surface.Translate(offset);

        // The child occupies the box it measured, placed by the offset above (ADR 0012). Given the whole space
        // instead, content that positions itself — right-aligned or right-to-left text — would be offset a second
        // time, off the far edge. Text re-wrapped at its own measured width reproduces the same lines: every line
        // already fits, and none can take a word more than it did in the wider box.
        context.RenderAllotted(Child, childPlan.Size, availableSpace.Height);

        context.Surface.Translate(offset.Reverse());
    }

    private float HorizontalOffset(float available, float child) => Horizontal switch
    {
        HorizontalPlacement.Center => (available - child) / 2,
        HorizontalPlacement.Right => available - child,
        _ => 0f
    };

    private float VerticalOffset(float available, float child) => Vertical switch
    {
        VerticalPlacement.Middle => (available - child) / 2,
        VerticalPlacement.Bottom => available - child,
        _ => 0f
    };
}
