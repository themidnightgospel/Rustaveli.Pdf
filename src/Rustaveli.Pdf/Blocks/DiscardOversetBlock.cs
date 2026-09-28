using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Draws as much of its child as fits where it first appears and discards the rest, the overset, rather than
/// continuing it onto the next page.
/// </summary>
/// <remarks>
/// Content that fits nowhere on the page takes no room and draws nothing, instead of pushing to a new page.
/// </remarks>
internal sealed class DiscardOversetBlock : EnclosingBlock
{
    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        Fit plan = base.PlanCore(availableSpace, context);

        if (plan.IsDeferred)
            return Fit.Complete(Extent.Zero);

        return plan.IsPartial ? Fit.Complete(plan.Size) : plan;
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (Child is null || Child.Plan(availableSpace, context.Planning).IsDeferred)
            return;

        Child.Render(availableSpace, context);
    }
}
