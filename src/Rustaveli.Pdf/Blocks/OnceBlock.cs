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

    public override Fit Plan(Extent availableSpace, PlanContext context) =>
        _hasRendered ? Fit.Nothing() : base.Plan(availableSpace, context);

    public override void Render(Extent availableSpace, RenderContext context)
    {
        if (_hasRendered)
            return;

        base.Render(availableSpace, context);
        _hasRendered = true;
    }
}
