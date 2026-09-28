using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Renders its child on the first page it appears on and nothing thereafter.
/// </summary>
/// <remarks>
/// Placed inside a header, this produces content that introduces a document once rather than repeating on
/// every page.
/// </remarks>
internal sealed class OnceBlock : EnclosingBlock
{
    private bool _hasRendered;

    protected override bool TracksDocumentProgress => true;

    protected override void ResetOwnState() => _hasRendered = false;

    protected override object? SaveOwnProgress() => _hasRendered;

    protected override void RestoreOwnProgress(object progress) => _hasRendered = (bool)progress;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        _hasRendered ? Fit.Nothing() : base.PlanCore(availableSpace, context);

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (_hasRendered)
            return;

        base.RenderCore(availableSpace, context);
        _hasRendered = true;
    }
}
