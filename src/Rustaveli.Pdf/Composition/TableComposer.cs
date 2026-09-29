using Rustaveli.Pdf.Blocks;

namespace Rustaveli.Pdf;

/// <summary>
/// Builds a table: its columns, body cells and optional repeating header and footer bands.
/// </summary>
public sealed class TableComposer
{
    private readonly TableBlock _block;

    internal TableComposer(TableBlock block) => _block = block;

    public void Columns(Action<TableColumns> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        handler(new TableColumns(_block));
    }

    /// <summary>Adds a body cell. Without an explicit position it is placed in the next free slot.</summary>
    public CellFrame Cell()
    {
        CellBlock tableCell = new CellBlock();
        _block.Cells.Add(tableCell);
        return new CellFrame(tableCell);
    }

    /// <summary>
    /// Stretches the last cell of every column down to the bottom of the table on each page, so a column that ends
    /// early, beside cells spanning further down, still reaches the table's foot.
    /// </summary>
    public void ExtendLastCellsToBottom() => _block.ExtendLastCells = true;

    /// <summary>Declares rows repeated at the top of every page the table spans.</summary>
    public void HeaderRows(Action<TableBand> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        handler(new TableBand(_block.HeaderCells));
    }

    /// <summary>Declares rows repeated at the bottom of every page the table spans.</summary>
    public void FooterRows(Action<TableBand> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        handler(new TableBand(_block.FooterCells));
    }

    /// <summary>
    /// Assigns positions to cells that did not specify one. Run once composition is complete, because a cell's
    /// span is only known after the caller has finished configuring it.
    /// </summary>
    internal void PlaceAutomaticCells()
    {
        // Cells without columns would pass placement as one column and then fail at export, saying only that the
        // columns do not fit — nothing about the columns never having been declared.
        if (_block.Columns.Count == 0 && (_block.Cells.Count > 0 || _block.HeaderCells.Count > 0 || _block.FooterCells.Count > 0))
            throw new CompositionException("A table declares its columns with Columns(...) before its cells can be placed.");

        int columnCount = _block.Columns.Count;
        CellPlacement.Apply(_block.Cells, columnCount, "body");
        CellPlacement.Apply(_block.HeaderCells, columnCount, "header");
        CellPlacement.Apply(_block.FooterCells, columnCount, "footer");
        Validate(_block.Cells, columnCount, "body");
        Validate(_block.HeaderCells, columnCount, "header");
        Validate(_block.FooterCells, columnCount, "footer");
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
