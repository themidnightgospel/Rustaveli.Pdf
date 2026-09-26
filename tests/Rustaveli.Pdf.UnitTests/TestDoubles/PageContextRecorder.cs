namespace Rustaveli.Pdf.UnitTests.TestDoubles;

/// <summary>
/// Occupies no space and records what the page context said every time it was drawn.
/// </summary>
/// <remarks>
/// The counting passes draw to a canvas that discards everything, so the only way to observe them is from
/// inside the element tree. Placed in a slot drawn on every page, this reveals each pass the engine made and the
/// page numbers it quoted while making it.
/// </remarks>
public sealed class PageContextRecorder : Block
{
    public List<(int CurrentPage, int TotalPages, bool IsDocumentLengthKnown)> Draws { get; } = [];

    public override Fit Plan(Extent availableSpace, PlanContext context) => Fit.Complete(Extent.Zero);

    public override void Render(Extent availableSpace, RenderContext context) =>
        Draws.Add((context.Page.CurrentPage, context.Page.TotalPages, context.Page.IsDocumentLengthKnown));
}
