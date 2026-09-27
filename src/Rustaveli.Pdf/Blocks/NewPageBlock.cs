using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Consumes the rest of the current page, pushing everything after it onto the next one.
/// </summary>
internal sealed class NewPageBlock : Block
{
    private bool _hasBroken;

    protected override void ResetOwnState() => _hasBroken = false;

    protected override object? SaveOwnProgress() => _hasBroken;

    protected override void RestoreOwnProgress(object progress) => _hasBroken = (bool)progress;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        _hasBroken
            ? Fit.Nothing()
            // Claiming the full remaining height forces the parent to treat the page as finished, and reporting
            // a partial render guarantees the engine comes back for the remainder on a fresh page.
            : Fit.Partial(new Extent(0, availableSpace.Height));

    protected override void RenderCore(Extent availableSpace, RenderContext context) => _hasBroken = true;
}
