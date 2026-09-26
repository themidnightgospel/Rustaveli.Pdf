namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// Content whose length depends on the page total it is told: two pages when the document is said to be one
/// page long, one page otherwise.
/// </summary>
/// <remarks>
/// Feeding each count back in produces the other count, so the total never settles. That is the pathological
/// document the cap on counting passes exists for. <see cref="Passes"/> counts the full resets the engine makes,
/// one at the start of every pass.
/// </remarks>
public sealed class OscillatingElement : Element
{
    private int _pagesDrawn;

    public int Passes { get; private set; }

    protected override void ResetOwnState()
    {
        _pagesDrawn = 0;
        Passes++;
    }

    public override SpacePlan Measure(Size availableSpace, LayoutContext context) =>
        _pagesDrawn + 1 < PagesNeeded(context.Page)
            ? SpacePlan.PartialRender(10, 10)
            : SpacePlan.FullRender(10, 10);

    public override void Draw(Size availableSpace, DrawContext context) => _pagesDrawn++;

    private static int PagesNeeded(PageContext page) =>
        page.IsDocumentLengthKnown && page.TotalPages == 1 ? 2 : 1;
}
