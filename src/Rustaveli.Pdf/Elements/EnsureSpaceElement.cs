using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Defers its content to the next page unless a minimum amount of room remains.
/// </summary>
/// <remarks>
/// Stops a heading or a short block being stranded in the last few points of a page. The requirement applies
/// only before the content starts: once it has begun, later pages continue it normally rather than demanding
/// the same headroom again.
/// </remarks>
public sealed class EnsureSpaceElement : EnclosingBlock
{
    private bool _hasStarted;

    /// <summary>The vertical room that must remain before the content will begin.</summary>
    public float MinHeight { get; set; }

    protected override void ResetOwnState() => _hasStarted = false;

    public override Fit Measure(Extent availableSpace, PlanContext context)
    {
        if (!_hasStarted && availableSpace.Height + Extent.Epsilon < MinHeight)
        {
            return Fit.Wrap(
                $"Only {availableSpace.Height:F1} points remain but {MinHeight:F1} was required before this content may start.");
        }

        return base.Measure(availableSpace, context);
    }

    public override void Draw(Extent availableSpace, RenderContext context)
    {
        // The headroom test is deliberately not repeated here. A parent decides by measuring, and it may then
        // legitimately draw with less height than it offered — a header band shrinks to the height it settled
        // on, a row draws its items at the row height. Re-deriving the decision from that smaller box would
        // refuse content the parent had already committed to, and drop it with no diagnostic.
        Fit plan = Child?.Measure(availableSpace, context.Layout) ?? Fit.FullRender(Extent.Zero);

        base.Draw(availableSpace, context);

        // Only content that actually occupied space counts as having started. Otherwise a page on which this
        // rendered nothing would permanently disarm the guarantee for every page after it.
        if (plan.DrewSomething && plan.Size.Height > Extent.Epsilon)
            _hasStarted = true;
    }
}
