using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Suppresses its child the first time it would render, and shows it on every occasion after that.
/// </summary>
/// <remarks>
/// The complement of <see cref="ShowOnceElement"/>: useful for a "continued" marker that should not appear on
/// the opening page.
/// </remarks>
public sealed class SkipOnceElement : ContainerElement
{
    private bool _hasSkipped;

    protected override bool TracksDocumentProgress => true;

    protected override void ResetOwnState() => _hasSkipped = false;

    public override SpacePlan Measure(Size availableSpace, LayoutContext context) =>
        _hasSkipped ? base.Measure(availableSpace, context) : SpacePlan.FullRender(Size.Zero);

    public override void Draw(Size availableSpace, DrawContext context)
    {
        if (_hasSkipped)
        {
            base.Draw(availableSpace, context);
            return;
        }

        _hasSkipped = true;
    }
}
