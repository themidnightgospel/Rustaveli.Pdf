using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Leaves its child out the first time it is drawn and shows it every time after: a "continued" note that belongs on
/// every page of a run but its first.
/// </summary>
internal sealed class SkipFirstBlock : EnclosingBlock
{
    private bool _skipped;

    /// <summary>Kept through the reset a running head gets for each page, or the note would skip every page.</summary>
    protected override bool TracksDocumentProgress => true;

    protected override void ResetOwnState() => _skipped = false;

    protected override object? SaveOwnProgress() => _skipped;

    protected override void RestoreOwnProgress(object progress) => _skipped = (bool)progress;

    /// <remarks>
    /// Until it has skipped, it is Complete and takes no room rather than Nothing: a parent passes over Nothing without
    /// drawing it, and only being drawn records that the first occurrence went by.
    /// </remarks>
    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        _skipped ? base.PlanCore(availableSpace, context) : Fit.Complete(Extent.Zero);

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (!_skipped)
        {
            _skipped = true;
            return;
        }

        base.RenderCore(availableSpace, context);
    }
}
