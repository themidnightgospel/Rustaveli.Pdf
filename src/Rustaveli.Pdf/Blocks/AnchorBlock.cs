using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Marks its child as a named destination that internal links can target.
/// </summary>
/// <remarks>
/// Registering the page number as a side effect of drawing is what lets a table of contents resolve targets:
/// the counting pass records where each section landed, and the drawing pass can then reference it.
/// </remarks>
internal sealed class AnchorBlock : EnclosingBlock
{
    public required string Name { get; init; }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        context.Pagination.RegisterAnchor(Name, context.Pagination.Folio);
        context.Surface.DrawDestination(Name);

        base.RenderCore(availableSpace, context);
    }
}
