using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// One item in a row of columns: the frame its content goes into, and how wide the row makes it.
/// </summary>
internal sealed class ColumnSlot : EnclosingBlock
{
    public ColumnSizing Sizing { get; set; } = ColumnSizing.Share;

    /// <summary>
    /// The width in points for a fixed item, or the weight of a sharing one; one unless set. A natural item has no use
    /// for it. In a grid, it is how many of the grid's columns the item spans.
    /// </summary>
    public float Value { get; set; } = 1f;
}
