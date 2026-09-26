using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Consumes the rest of the current page, pushing everything after it onto the next one.
/// </summary>
public sealed class PageBreakElement : Block
{
    private bool _hasBroken;

    protected override void ResetOwnState() => _hasBroken = false;

    public override Fit Measure(Extent availableSpace, PlanContext context) =>
        _hasBroken
            ? Fit.Empty()
            // Claiming the full remaining height forces the parent to treat the page as finished, and reporting
            // a partial render guarantees the engine comes back for the remainder on a fresh page.
            : Fit.PartialRender(new Extent(0, availableSpace.Height));

    public override void Draw(Extent availableSpace, RenderContext context) => _hasBroken = true;
}
