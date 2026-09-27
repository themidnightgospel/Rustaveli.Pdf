using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Measures its child against unlimited space, letting it exceed what the parent offered.
/// </summary>
/// <remarks>
/// Reports zero size to its parent, so surrounding content lays out as though nothing were here. Useful for
/// overlays and annotations that should not disturb the flow they sit in.
/// </remarks>
internal sealed class UnboundedBlock : EnclosingBlock
{
    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        Fit childPlan = Child?.Plan(Extent.Max, context) ?? Fit.Complete(Extent.Zero);

        if (childPlan.IsNothing)
            return Fit.Nothing();

        // Even unbounded space has a ceiling — the largest page PDF allows. Content that cannot fit inside that
        // is reported rather than swallowed, otherwise Measure would promise a render that Draw silently skips.
        if (childPlan.IsDeferred)
            return childPlan;

        if (childPlan.IsPartial)
            return Fit.Defer("Unbounded content does not fit even on the largest page, so the rest would be lost.");

        return Fit.Complete(Extent.Zero);
    }

    public override void Render(Extent availableSpace, RenderContext context)
    {
        Fit childPlan = Child?.Plan(Extent.Max, context.Planning) ?? Fit.Complete(Extent.Zero);

        if (childPlan.IsDeferred || childPlan.IsNothing)
            return;

        Child?.Render(childPlan.Size, context);
    }
}
