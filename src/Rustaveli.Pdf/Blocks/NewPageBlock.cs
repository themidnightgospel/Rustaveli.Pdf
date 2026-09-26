using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Consumes the rest of the current page, pushing everything after it onto the next one.
/// </summary>
internal sealed class NewPageBlock : Block
{
    private bool _hasBroken;

    protected override void ResetOwnState() => _hasBroken = false;

    public override Fit Plan(Extent availableSpace, PlanContext context) =>
        _hasBroken
            ? Fit.Nothing()
            // Claiming the full remaining height forces the parent to treat the page as finished, and reporting
            // a partial render guarantees the engine comes back for the remainder on a fresh page.
            : Fit.Partial(new Extent(0, availableSpace.Height));

    public override void Render(Extent availableSpace, RenderContext context) => _hasBroken = true;
}
