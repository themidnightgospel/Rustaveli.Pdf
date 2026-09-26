using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Refuses to split its child across pages, deferring the whole thing rather than drawing part of it.
/// </summary>
/// <remarks>
/// Converts a partial render into a wrap, which sends the content to the next page intact. If it cannot fit on
/// an empty page either, the engine reports a layout failure rather than silently truncating.
/// </remarks>
public sealed class KeepTogetherBlock : EnclosingBlock
{
    public override Fit Plan(Extent availableSpace, PlanContext context)
    {
        Fit childPlan = base.Plan(availableSpace, context);

        return childPlan.IsPartial
            ? Fit.Defer("The content is kept together and does not fit in the remaining space.")
            : childPlan;
    }

    public override void Render(Extent availableSpace, RenderContext context)
    {
        // Measure guarantees the parent only draws this when the whole child fits.
        if (Plan(availableSpace, context.Layout).IsDeferred)
            return;

        base.Render(availableSpace, context);
    }
}
