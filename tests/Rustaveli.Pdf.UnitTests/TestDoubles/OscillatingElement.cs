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
public sealed class OscillatingElement : Block
{
    private int _pagesDrawn;

    public int Passes { get; private set; }

    protected override void ResetOwnState()
    {
        _pagesDrawn = 0;
        Passes++;
    }

    public override Fit Measure(Extent availableSpace, PlanContext context) =>
        _pagesDrawn + 1 < PagesNeeded(context.Page)
            ? Fit.PartialRender(10, 10)
            : Fit.FullRender(10, 10);

    public override void Draw(Extent availableSpace, RenderContext context) => _pagesDrawn++;

    private static int PagesNeeded(Pagination page) =>
        page.IsDocumentLengthKnown && page.TotalPages == 1 ? 2 : 1;
}
