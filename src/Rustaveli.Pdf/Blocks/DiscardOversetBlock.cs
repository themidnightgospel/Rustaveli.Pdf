using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Draws as much of its child as fits where the child first appears and discards the rest. It never runs on to
/// another page, and never sends content to a fresh one.
/// </summary>
internal sealed class DiscardOversetBlock : EnclosingBlock
{
    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        Fit plan = base.PlanCore(availableSpace, context);

        return plan.Kind switch
        {
            // Content that would move to a fresh page is dropped here instead, taking no room.
            FitKind.Defer => Fit.Complete(Extent.Zero),

            // What fits is the whole of it, as far as the parent is concerned; the rest is never drawn.
            FitKind.Partial => Fit.Complete(plan.Size),
            _ => plan,
        };
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (Child is null || Child.Plan(availableSpace, context.Planning).IsDeferred)
            return;

        Child.Render(availableSpace, context);
    }
}
