using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Shows its child only while a condition holds: one fixed when the document was composed, one asked afresh on every
/// page, or both.
/// </summary>
internal sealed class WhenBlock : EnclosingBlock
{
    /// <summary>The fixed part of the condition; true unless set otherwise.</summary>
    public bool Condition { get; set; } = true;

    /// <summary>The part of the condition asked of each page, or null to ask nothing of it.</summary>
    public Func<PageFacts, bool>? OnPage { get; set; }

    /// <remarks>
    /// While the condition does not hold, the block is Complete and takes no room. Nothing would say its content is
    /// used up, when it may well show on a later page.
    /// </remarks>
    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        Holds(context.Pagination) ? base.PlanCore(availableSpace, context) : Fit.Complete(Extent.Zero);

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (Holds(context.Pagination))
            base.RenderCore(availableSpace, context);
    }

    private bool Holds(Pagination pagination)
    {
        if (!Condition)
            return false;

        if (OnPage is null)
            return true;

        // The page count is asked for only once it is settled, since asking marks the pass as depending on it.
        int? pageCount = pagination.IsPageCountKnown ? pagination.PageCount : null;
        return OnPage(new PageFacts(pagination.Folio, pageCount));
    }
}
