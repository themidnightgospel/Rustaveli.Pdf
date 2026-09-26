using Rustaveli.Pdf.Elements;
using Rustaveli.Pdf.Exceptions;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Builds a table: its columns, body cells and optional repeating header and footer bands.
/// </summary>
public sealed class TableDescriptor(TableElement element)
{
    public void ColumnsDefinition(Action<TableColumnsDefinitionDescriptor> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, "handler");
        handler(new TableColumnsDefinitionDescriptor(element));
    }

    /// <summary>Adds a body cell. Without an explicit position it is placed in the next free slot.</summary>
    public TableCellDescriptor Cell()
    {
        TableCell tableCell = new TableCell();
        element.Cells.Add(tableCell);
        return new TableCellDescriptor(tableCell);
    }

    /// <summary>Declares rows repeated at the top of every page the table spans.</summary>
    public void Header(Action<TableBandDescriptor> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, "handler");
        handler(new TableBandDescriptor(element.HeaderCells));
    }

    /// <summary>Declares rows repeated at the bottom of every page the table spans.</summary>
    public void Footer(Action<TableBandDescriptor> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, "handler");
        handler(new TableBandDescriptor(element.FooterCells));
    }

    /// <summary>
    /// Assigns positions to cells that did not specify one. Run once composition is complete, because a cell's
    /// span is only known after the caller has finished configuring it.
    /// </summary>
    internal void PlaceAutomaticCells()
    {
        int columnCount = Math.Max(1, element.Columns.Count);
        AutoPlacement.Apply(element.Cells, columnCount);
        AutoPlacement.Apply(element.HeaderCells, columnCount);
        AutoPlacement.Apply(element.FooterCells, columnCount);
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
    private static void Validate(List<TableCell> cells, int columnCount, string band)
    {
        foreach (TableCell cell in cells)
        {
            if (cell.LastColumn <= columnCount)
            {
                continue;
            }
            throw new DocumentComposeException($"A {band} cell occupies columns {cell.Column} to {cell.LastColumn}, but the table declares only {columnCount}. Add more columns, or reduce the cell's column or span.");
        }
    }
}
