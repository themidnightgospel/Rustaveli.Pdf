using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Renders its child on the first page it appears on and nothing thereafter.
/// </summary>
/// <remarks>
/// Placed inside a header, this produces content that introduces a document once rather than repeating on
/// every page.
/// </remarks>
public sealed class ShowOnceElement : EnclosingBlock
{
    private bool _hasRendered;

    protected override bool TracksDocumentProgress => true;

    protected override void ResetOwnState() => _hasRendered = false;

    public override Fit Measure(Extent availableSpace, PlanContext context) =>
        _hasRendered ? Fit.Empty() : base.Measure(availableSpace, context);

    public override void Draw(Extent availableSpace, RenderContext context)
    {
        if (_hasRendered)
            return;

        base.Draw(availableSpace, context);
        _hasRendered = true;
    }
}
