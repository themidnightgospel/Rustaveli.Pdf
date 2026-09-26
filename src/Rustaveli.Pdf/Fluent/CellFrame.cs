using Rustaveli.Pdf.Elements;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Positions a single table cell and exposes it as a container for content.
/// </summary>
public sealed class CellFrame(CellBlock cell) : IFrame
{
    Block? IFrame.Child
    {
        get => cell.Child;
        set => cell.Child = value;
    }

    public CellFrame Row(int row)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(row, 1);

        cell.Row = row;
        cell.HasExplicitRow = true;
        return this;
    }

    public CellFrame Column(int column)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);

        cell.Column = column;
        cell.HasExplicitColumn = true;
        return this;
    }

    public CellFrame RowSpan(int span)
    {
        cell.RowSpan = Math.Max(1, span);
        return this;
    }

    public CellFrame ColumnSpan(int span)
    {
        cell.ColumnSpan = Math.Max(1, span);
        return this;
    }
}
