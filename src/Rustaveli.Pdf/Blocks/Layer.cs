using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>One layer of a <see cref="LayersBlock"/>: the frame its content goes into.</summary>
internal sealed class Layer : EnclosingBlock
{
    /// <summary>
    /// Whether this is the base layer, whose size the layers take and whose content flows from page to page.
    /// </summary>
    public bool IsBase { get; set; }
}
