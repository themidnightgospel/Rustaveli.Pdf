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

    /// <summary>Whether the cell heads its row, in a tagged table.</summary>
    public bool HeadsRow { get; set; }

    /// <summary>Set when the caller pinned the row; placement keeps it and chooses only an unpinned column.</summary>
    internal bool HasExplicitRow { get; set; }

    /// <summary>Set when the caller pinned the column; placement keeps it and chooses only an unpinned row.</summary>
    internal bool HasExplicitColumn { get; set; }

    internal int LastRow => Row + RowSpan - 1;

    internal int LastColumn => Column + ColumnSpan - 1;
}
