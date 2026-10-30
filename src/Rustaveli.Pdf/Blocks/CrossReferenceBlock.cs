using Rustaveli.Pdf.Drawing;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Makes the area occupied by its child clickable, jumping to a named section of the document.
/// </summary>
internal sealed class CrossReferenceBlock : LinkingBlock
{
    public string Anchor { get; set; } = string.Empty;

    protected override void AddLink(ISurface surface, Extent area)
    {
        if (!string.IsNullOrEmpty(Anchor))
            surface.LinkToDestination(Anchor, Offset.Zero, area);
    }
}
