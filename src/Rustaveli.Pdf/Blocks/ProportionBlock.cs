using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Forces its child into a fixed width-to-height ratio.
/// </summary>
internal sealed class ProportionBlock : EnclosingBlock
{
    /// <summary>Width divided by height. Must be greater than zero.</summary>
    public float Ratio { get; set; } = 1f;

    public ProportionFit Fit { get; set; } = ProportionFit.Width;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        if (Ratio <= 0)
            return Layout.Fit.Defer("The proportion must be greater than zero.");

        Extent size = ResolveSize(availableSpace);

        if (!size.FitsIn(availableSpace))
            return Layout.Fit.Defer("The space available is too small for the requested proportion.");

        Fit childPlan = Child?.Plan(size, context) ?? Layout.Fit.Complete(Extent.Zero);

        if (childPlan.IsDeferred)
            return childPlan;

        if (childPlan.IsNothing)
            return Layout.Fit.Nothing();

        return childPlan.IsComplete ? Layout.Fit.Complete(size) : Layout.Fit.Partial(size);
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context) =>
        Child?.Render(ResolveSize(availableSpace), context);

    private Extent ResolveSize(Extent availableSpace)
    {
        Extent fromWidth = new Extent(availableSpace.Width, availableSpace.Width / Ratio);
        Extent fromHeight = new Extent(availableSpace.Height * Ratio, availableSpace.Height);

        return Fit switch
        {
            ProportionFit.Width => fromWidth,
            ProportionFit.Height => fromHeight,
            // Pick whichever axis binds first so the result stays inside the offered space.
            ProportionFit.Area => fromWidth.Height <= availableSpace.Height ? fromWidth : fromHeight,
            _ => fromWidth
        };
    }
}
