using Rustaveli.Pdf.Drawing;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Makes the area occupied by its child clickable, opening an external URL.
/// </summary>
internal sealed class LinkBlock : LinkingBlock
{
    public string Url { get; set; } = string.Empty;

    protected override void AddLink(ISurface surface, Extent area)
    {
        if (!string.IsNullOrEmpty(Url))
            surface.LinkToUrl(Url, Offset.Zero, area);
    }
}
