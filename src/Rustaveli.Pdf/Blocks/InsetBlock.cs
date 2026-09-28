using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Insets its child by a fixed amount on each side.
/// </summary>
internal sealed class InsetBlock : EnclosingBlock
{
    public Sides Inset { get; set; } = Sides.Zero;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        Extent innerSpace = new Extent(
            availableSpace.Width - Inset.Horizontal,
            availableSpace.Height - Inset.Vertical);

        if (innerSpace.IsNegative)
            return Fit.Defer("The space available is smaller than the inset.");

        Fit childPlan = Child?.Plan(innerSpace, context) ?? Fit.Complete(Extent.Zero);

        if (childPlan.IsDeferred)
            return childPlan;

        // A child with nothing left to draw must not resurrect the padding on the next page.
        if (childPlan.IsNothing)
            return Fit.Nothing();

        Extent size = new Extent(
            childPlan.Size.Width + Inset.Horizontal,
            childPlan.Size.Height + Inset.Vertical);

        return childPlan.IsComplete ? Fit.Complete(size) : Fit.Partial(size);
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (Child is null)
            return;

        Extent innerSpace = new Extent(
            availableSpace.Width - Inset.Horizontal,
            availableSpace.Height - Inset.Vertical);

        if (innerSpace.IsNegative)
            return;

        context.Surface.Translate(new Offset(Inset.Left, Inset.Top));
        Child.Render(innerSpace, context);
        context.Surface.Translate(new Offset(Inset.Left, Inset.Top).Reverse());
    }
}
