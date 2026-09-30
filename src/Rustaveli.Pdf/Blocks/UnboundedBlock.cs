using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Lets its child take all the room it wants while telling its parent it takes none, so the content around it is laid
/// out as if it were not there: the place for an overlay or an annotation.
/// </summary>
internal sealed class UnboundedBlock : EnclosingBlock
{
    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        if (Child is null)
            return Fit.Complete(Extent.Zero);

        Fit plan = Child.Plan(Extent.Max, context);

        return plan.Kind switch
        {
            FitKind.Complete => Fit.Complete(Extent.Zero),

            // The room offered is already the largest page there is, so the rest has nowhere further to go.
            FitKind.Partial => Fit.Defer("Unbounded content does not fit even on the largest page, so the rest would be lost."),
            _ => plan,
        };
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (Child is null)
            return;

        Fit plan = Child.Plan(Extent.Max, context.Planning);

        if (!plan.PlacesContent)
            return;

        context.RenderAllotted(Child, plan.Size, Extent.Max.Height);
    }
}
