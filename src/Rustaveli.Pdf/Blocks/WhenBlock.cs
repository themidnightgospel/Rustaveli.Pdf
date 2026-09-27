using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Renders its child only if a condition holds, collapsing to nothing otherwise. The condition is either fixed, or
/// asked of each page the child would appear on.
/// </summary>
internal sealed class WhenBlock : EnclosingBlock
{
    public bool Condition { get; set; } = true;

    /// <summary>Asked of every page, in addition to <see cref="Condition"/>, when set.</summary>
    public Func<PageFacts, bool>? OnPage { get; set; }

    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        Holds(context.Pagination) ? base.PlanCore(availableSpace, context) : Fit.Complete(Extent.Zero);

    public override void Render(Extent availableSpace, RenderContext context)
    {
        if (Holds(context.Planning.Pagination))
            base.Render(availableSpace, context);
    }

    private bool Holds(Pagination pagination) =>
        Condition
        && (OnPage is null || OnPage(new PageFacts(pagination.Folio, pagination.IsPageCountKnown ? pagination.PageCount : null)));
}
