using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Defers its content to the next page unless a minimum amount of room remains.
/// </summary>
/// <remarks>
/// Stops a heading or a short block being stranded in the last few points of a page. The requirement applies
/// only before the content starts: once it has begun, later pages continue it normally rather than demanding
/// the same headroom again.
/// </remarks>
internal sealed class RequireSpaceBlock : EnclosingBlock
{
    private bool _hasStarted;

    /// <summary>The vertical room that must remain before the content will begin.</summary>
    public float MinHeight { get; set; }

    protected override void ResetOwnState() => _hasStarted = false;

    protected override object? SaveOwnProgress() => _hasStarted;

    protected override void RestoreOwnProgress(object progress) => _hasStarted = (bool)progress;

    public override Fit Plan(Extent availableSpace, PlanContext context)
    {
        if (!_hasStarted && availableSpace.Height + Extent.Epsilon < MinHeight)
        {
            return Fit.Defer(
                $"Only {availableSpace.Height:F1} points remain but {MinHeight:F1} was required before this content may start.");
        }

        return base.Plan(availableSpace, context);
    }

    public override void Render(Extent availableSpace, RenderContext context)
    {
        // The headroom test is deliberately not repeated here. A parent decides by measuring, and it may then
        // legitimately draw with less height than it offered — a header band shrinks to the height it settled
        // on, a row draws its items at the row height. Re-deriving the decision from that smaller box would
        // refuse content the parent had already committed to, and drop it with no diagnostic.
        Fit plan = Child?.Plan(availableSpace, context.Planning) ?? Fit.Complete(Extent.Zero);

        base.Render(availableSpace, context);

        // Only content that actually occupied space counts as having started. Otherwise a page on which this
        // rendered nothing would permanently disarm the guarantee for every page after it.
        if (plan.PlacesContent && plan.Size.Height > Extent.Epsilon)
            _hasStarted = true;
    }
}
