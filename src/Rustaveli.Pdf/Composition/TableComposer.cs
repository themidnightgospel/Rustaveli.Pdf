using Rustaveli.Pdf.Blocks;

namespace Rustaveli.Pdf;

/// <summary>
/// Declares a table's columns and adds its cells: those of the body, and those of header and footer rows drawn again
/// on every page the table runs on to.
/// </summary>
/// <remarks>
/// One set of columns serves the body, the header and the footer alike. A cell not given a row or a column is placed
/// in the next slot free, left to right and then top to bottom, each band laid out on its own. The cells are placed
/// and checked against the columns as soon as the table is composed, so a table that cannot be laid out fails where it
/// is written rather than when the document is exported.
/// </remarks>
public sealed class TableComposer
{
    private readonly TableBlock _table;
    private readonly TableBand _body;
    private readonly TableBand _header;
    private readonly TableBand _footer;

    internal TableComposer(TableBlock table)
    {
        _table = table;
        _body = new TableBand(table.Cells);
        _header = new TableBand(table.HeaderCells);
        _footer = new TableBand(table.FooterCells);
    }

    /// <summary>Declares columns, after any declared already.</summary>
    public void Columns(Action<TableColumns> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        handler(new TableColumns(_table));
    }

    /// <summary>Adds a cell to the body of the table.</summary>
    public CellFrame Cell() => _body.Cell();

    /// <summary>
    /// Stretches, on every page, each cell that is the last in every column it covers down to the bottom of the rows on
    /// that page, so that a column ending early still reaches the foot of the table.
    /// </summary>
    public void ExtendLastCellsToBottom() => _table.ExtendLastCells = true;

    /// <summary>Adds cells to the rows drawn at the top of every page the table runs on to, after any added already.</summary>
    public void HeaderRows(Action<TableBand> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        handler(_header);
    }

    /// <summary>Adds cells to the rows drawn at the bottom of every page the table runs on to, after any added already.</summary>
    public void FooterRows(Action<TableBand> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        handler(_footer);
    }

    /// <summary>
    /// Gives every cell not placed by hand its slot, and makes sure every cell lies within the columns declared: run
    /// once, when the composing of the table is done.
    /// </summary>
    /// <exception cref="CompositionException">
    /// The table has cells but no columns, or a cell reaches past the last column.
    /// </exception>
    internal void PlaceAutomaticCells()
    {
        int columns = _table.Columns.Count;
        (List<CellBlock> Cells, string Name)[] bands =
        [
            (_table.Cells, "body"),
            (_table.HeaderCells, "header"),
            (_table.FooterCells, "footer"),
        ];

        if (columns == 0)
        {
            // A table of nothing at all is harmless, and draws nothing.
            foreach ((List<CellBlock> cells, string _) in bands)
            {
                if (cells.Count > 0)
                    throw new CompositionException("A table declares its columns with Columns(...) before its cells can be placed.");
            }

            return;
        }

        foreach ((List<CellBlock> cells, string name) in bands)
            CellPlacement.Apply(cells, columns, name);

        foreach ((List<CellBlock> cells, string name) in bands)
        {
            foreach (CellBlock cell in cells)
            {
                if (cell.LastColumn > columns)
                {
                    throw new CompositionException(
                        $"A {name} cell occupies columns {cell.Column} to {cell.LastColumn}, but the table declares only {columns}. " +
                        "Add more columns, or reduce the cell's column or span.");
                }
            }
        }
    }
}
