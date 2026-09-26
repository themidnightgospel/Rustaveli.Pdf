using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// A single column of a <see cref="ColumnsBlock"/>.
/// </summary>
internal sealed class ColumnSlot : EnclosingBlock
{
    public ColumnSizing Sizing { get; set; } = ColumnSizing.Relative;

    /// <summary>A width in points for constant items, or a weight for relative items.</summary>
    public float Value { get; set; } = 1f;
}
