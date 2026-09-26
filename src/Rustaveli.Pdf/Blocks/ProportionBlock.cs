using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Forces its child into a fixed width-to-height ratio.
/// </summary>
internal sealed class ProportionBlock : EnclosingBlock
{
    /// <summary>Width divided by height. Must be greater than zero.</summary>
    public float Ratio { get; set; } = 1f;

    public ProportionFit Option { get; set; } = ProportionFit.Width;

    public override Fit Plan(Extent availableSpace, PlanContext context)
    {
        if (Ratio <= 0)
            return Fit.Defer("The aspect ratio must be greater than zero.");

        Extent size = ResolveSize(availableSpace);

        if (!size.FitsIn(availableSpace))
            return Fit.Defer("The available space is too small for the requested aspect ratio.");

        Fit childPlan = Child?.Plan(size, context) ?? Fit.Complete(Extent.Zero);

        if (childPlan.IsDeferred)
            return childPlan;

        if (childPlan.IsNothing)
            return Fit.Nothing();

        return childPlan.IsComplete ? Fit.Complete(size) : Fit.Partial(size);
    }

    public override void Render(Extent availableSpace, RenderContext context) =>
        Child?.Render(ResolveSize(availableSpace), context);

    private Extent ResolveSize(Extent availableSpace)
    {
        Extent fromWidth = new Extent(availableSpace.Width, availableSpace.Width / Ratio);
        Extent fromHeight = new Extent(availableSpace.Height * Ratio, availableSpace.Height);

        return Option switch
        {
            ProportionFit.Width => fromWidth,
            ProportionFit.Height => fromHeight,
            // Pick whichever axis binds first so the result stays inside the offered space.
            ProportionFit.Area => fromWidth.Height <= availableSpace.Height ? fromWidth : fromHeight,
            _ => fromWidth
        };
    }
}
