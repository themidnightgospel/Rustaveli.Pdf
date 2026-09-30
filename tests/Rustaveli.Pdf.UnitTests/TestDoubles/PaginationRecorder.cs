namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// Occupies no space and records what the page context said every time it was drawn.
/// </summary>
/// <remarks>
/// The counting passes draw to a surface that discards everything, so the only way to observe them is from
/// inside the block tree. Placed in a slot drawn on every page, this reveals each pass the engine made and the
/// page numbers it quoted while making it.
/// </remarks>
internal sealed class PaginationRecorder : Block
{
    public List<(int CurrentPage, int TotalPages, bool IsDocumentLengthKnown)> Draws { get; } = [];

    protected override Fit PlanCore(Extent availableSpace, PlanContext context) => Fit.Complete(Extent.Zero);

    protected override void RenderCore(Extent availableSpace, RenderContext context) =>
        Draws.Add((context.Pagination.Folio, context.Pagination.PageCount, context.Pagination.IsPageCountKnown));
}
