using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Refuses to split its child across pages, deferring the whole thing rather than drawing part of it.
/// </summary>
/// <remarks>
/// Converts a partial render into a wrap, which sends the content to the next page intact. If it cannot fit on
/// an empty page either, the engine reports a layout failure rather than silently truncating.
/// </remarks>
internal sealed class KeepTogetherBlock : EnclosingBlock
{
    /// <summary>
    /// Whether content too long for any page is split rather than refused: kept together only where that is possible.
    /// </summary>
    public bool WherePossible { get; init; }

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        Fit childPlan = base.PlanCore(availableSpace, context);

        if (!childPlan.IsPartial)
            return childPlan;

        // Moved to a fresh page, would it fit whole there? Only then is moving it worth a page; content longer than
        // any page is split where it is, as it would be anyway. The page body is only an estimate of the room a fresh
        // page offers — an inset or a band around the content takes some of it — so a page that cannot start at all
        // unless this splits says it can never fit whole.
        if (WherePossible
            && (context.SplitsWherePossible
                || availableSpace.Height >= context.PageBody.Height - Extent.Epsilon
                || !base.PlanCore(new Extent(availableSpace.Width, context.PageBody.Height), context).IsComplete))
        {
            return childPlan;
        }

        return Fit.Defer("The content is kept together and does not fit in the remaining space.");
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        // Measure guarantees the parent only draws this when it is to be drawn here, whole or, where possible, not.
        if (Plan(availableSpace, context.Planning).IsDeferred)
            return;

        base.RenderCore(availableSpace, context);
    }
}
