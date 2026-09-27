using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Suppresses its child the first time it would render, and shows it on every occasion after that.
/// </summary>
/// <remarks>
/// The complement of <see cref="OnceBlock"/>: useful for a "continued" marker that should not appear on
/// the opening page.
/// </remarks>
internal sealed class SkipFirstBlock : EnclosingBlock
{
    private bool _hasSkipped;

    protected override bool TracksDocumentProgress => true;

    protected override void ResetOwnState() => _hasSkipped = false;

    protected override object? SaveOwnProgress() => _hasSkipped;

    protected override void RestoreOwnProgress(object progress) => _hasSkipped = (bool)progress;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        _hasSkipped ? base.PlanCore(availableSpace, context) : Fit.Complete(Extent.Zero);

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (_hasSkipped)
        {
            base.RenderCore(availableSpace, context);
            return;
        }

        _hasSkipped = true;
    }
}
