using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Suppresses its child the first time it would render, and shows it on every occasion after that.
/// </summary>
/// <remarks>
/// The complement of <see cref="OnceBlock"/>: useful for a "continued" marker that should not appear on
/// the opening page.
/// </remarks>
public sealed class SkipFirstBlock : EnclosingBlock
{
    private bool _hasSkipped;

    protected override bool TracksDocumentProgress => true;

    protected override void ResetOwnState() => _hasSkipped = false;

    public override Fit Plan(Extent availableSpace, PlanContext context) =>
        _hasSkipped ? base.Plan(availableSpace, context) : Fit.Complete(Extent.Zero);

    public override void Render(Extent availableSpace, RenderContext context)
    {
        if (_hasSkipped)
        {
            base.Render(availableSpace, context);
            return;
        }

        _hasSkipped = true;
    }
}
