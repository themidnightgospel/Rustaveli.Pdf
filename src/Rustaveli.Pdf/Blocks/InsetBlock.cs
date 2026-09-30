using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Sets its child in from each side of its room by a fixed amount, and takes that much room around the child. An
/// amount below zero sets the child out past that side instead.
/// </summary>
internal sealed class InsetBlock : EnclosingBlock
{
    public Sides Inset { get; set; } = Sides.Zero;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        Extent inner = Inner(availableSpace);

        if (inner.IsNegative)
            return Fit.Defer("The space available is smaller than the inset.");

        // Content used up stays Nothing, so the inset does not linger as a band of blank room on later pages.
        Fit plan = base.PlanCore(inner, context);
        return Resized(plan, new Extent(plan.Size.Width + Inset.Horizontal, plan.Size.Height + Inset.Vertical));
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        Extent inner = Inner(availableSpace);

        if (Child is null || inner.IsNegative)
            return;

        Offset corner = new Offset(Inset.Left, Inset.Top);
        context.Surface.MoveOrigin(corner);
        Child.Render(inner, context);
        context.Surface.MoveOrigin(corner.Reverse());
    }

    private Extent Inner(Extent room) => new Extent(room.Width - Inset.Horizontal, room.Height - Inset.Vertical);
}
