using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Consumes the rest of the current page, pushing everything after it onto the next one.
/// </summary>
public sealed class PageBreakElement : Element
{
    private bool _hasBroken;

    protected override void ResetOwnState() => _hasBroken = false;

    public override SpacePlan Measure(Size availableSpace, LayoutContext context) =>
        _hasBroken
            ? SpacePlan.Empty()
            // Claiming the full remaining height forces the parent to treat the page as finished, and reporting
            // a partial render guarantees the engine comes back for the remainder on a fresh page.
            : SpacePlan.PartialRender(new Size(0, availableSpace.Height));

    public override void Draw(Size availableSpace, DrawContext context) => _hasBroken = true;
}
