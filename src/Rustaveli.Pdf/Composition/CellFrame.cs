using Rustaveli.Pdf.Blocks;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf;

/// <summary>
/// Positions a single table cell and exposes it as a container for content.
/// </summary>
public sealed class CellFrame : IFrameSlot
{
    private readonly CellBlock _cell;

    internal CellFrame(CellBlock cell) => _cell = cell;

    Block? IFrameSlot.Child
    {
        get => _cell.Child;
        set => _cell.Child = value;
    }

    public CellFrame AtRow(int row)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(row, 1);

        _cell.Row = row;
        _cell.HasExplicitRow = true;
        return this;
    }

    public CellFrame AtColumn(int column)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);

        _cell.Column = column;
        _cell.HasExplicitColumn = true;
        return this;
    }

    public CellFrame SpanRows(int span)
    {
        _cell.RowSpan = Math.Max(1, span);
        return this;
    }

    public CellFrame SpanColumns(int span)
    {
        _cell.ColumnSpan = Math.Max(1, span);
        return this;
    }

    /// <summary>
    /// Makes the cell the heading of its row, as the cells of header rows head their columns, when a table tagged
    /// <see cref="ContentTag.Table"/> is exported tagged.
    /// </summary>
    public CellFrame RowHeading()
    {
        _cell.HeadsRow = true;
        return this;
    }
}
