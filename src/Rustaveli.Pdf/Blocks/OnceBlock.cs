using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Shows its child on the first page it is drawn on and on no page after it in the same pass: in a running head, a
/// title that introduces the document once.
/// </summary>
internal sealed class OnceBlock : EnclosingBlock
{
    private bool _shown;

    /// <summary>
    /// A running head is prepared afresh for every page, and having been shown is a fact about the document rather
    /// than the page, so it outlasts that.
    /// </summary>
    protected override bool TracksDocumentProgress => true;

    protected override void ResetOwnState() => _shown = false;

    protected override object? SaveOwnProgress() => _shown;

    protected override void RestoreOwnProgress(object progress) => _shown = (bool)progress;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        _shown ? Fit.Nothing() : base.PlanCore(availableSpace, context);

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (_shown)
            return;

        base.RenderCore(availableSpace, context);

        // Shown once is shown: content that continues past this page does not continue here.
        _shown = true;
    }
}
