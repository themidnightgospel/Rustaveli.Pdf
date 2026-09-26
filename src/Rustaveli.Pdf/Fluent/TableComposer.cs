using Rustaveli.Pdf.Elements;
using Rustaveli.Pdf.Exceptions;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Builds a table: its columns, body cells and optional repeating header and footer bands.
/// </summary>
public sealed class TableComposer(TableElement element)
{
    public void ColumnsDefinition(Action<TableColumns> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, "handler");
        handler(new TableColumns(element));
    }

    /// <summary>Adds a body cell. Without an explicit position it is placed in the next free slot.</summary>
    public CellFrame Cell()
    {
        CellBlock tableCell = new CellBlock();
        element.Cells.Add(tableCell);
        return new CellFrame(tableCell);
    }

    /// <summary>Declares rows repeated at the top of every page the table spans.</summary>
    public void Header(Action<TableBand> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, "handler");
        handler(new TableBand(element.HeaderCells));
    }

    /// <summary>Declares rows repeated at the bottom of every page the table spans.</summary>
    public void Footer(Action<TableBand> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, "handler");
        handler(new TableBand(element.FooterCells));
    }

    /// <summary>
    /// Assigns positions to cells that did not specify one. Run once composition is complete, because a cell's
    /// span is only known after the caller has finished configuring it.
    /// </summary>
    internal void PlaceAutomaticCells()
    {
        int columnCount = Math.Max(1, element.Columns.Count);
        CellPlacement.Apply(element.Cells, columnCount);
        CellPlacement.Apply(element.HeaderCells, columnCount);
        CellPlacement.Apply(element.FooterCells, columnCount);
        Validate(element.Cells, columnCount, "body");
        Validate(element.HeaderCells, columnCount, "header");
        Validate(element.FooterCells, columnCount, "footer");
    }

    /// <summary>
    /// Rejects cells that fall outside the declared columns, while the composing code is still on the stack.
    /// </summary>
    /// <remarks>
    /// Left unchecked these surface much later as an <see cref="System.IndexOutOfRangeException" /> from deep inside
    /// the layout engine, which says nothing about the cell that caused it.
    /// </remarks>
    private static void Validate(List<CellBlock> cells, int columnCount, string band)
    {
        foreach (CellBlock cell in cells)
        {
            if (cell.LastColumn <= columnCount)
            {
                continue;
            }
            throw new CompositionException($"A {band} cell occupies columns {cell.Column} to {cell.LastColumn}, but the table declares only {columnCount}. Add more columns, or reduce the cell's column or span.");
        }
    }
}
