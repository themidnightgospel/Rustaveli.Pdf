using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// A cell occupying one or more rows and columns of a table.
/// </summary>
internal sealed class CellBlock : EnclosingBlock
{
    /// <summary>One-based index of the topmost row this cell occupies.</summary>
    public int Row { get; set; } = 1;

    /// <summary>One-based index of the leftmost column this cell occupies.</summary>
    public int Column { get; set; } = 1;

    public int RowSpan { get; set; } = 1;

    public int ColumnSpan { get; set; } = 1;

    /// <summary>Set when the caller pinned the row, which excludes this cell from automatic placement.</summary>
    internal bool HasExplicitRow { get; set; }

    /// <summary>Set when the caller pinned the column, which excludes this cell from automatic placement.</summary>
    internal bool HasExplicitColumn { get; set; }

    internal int LastRow => Row + Math.Max(1, RowSpan) - 1;

    internal int LastColumn => Column + Math.Max(1, ColumnSpan) - 1;
}
