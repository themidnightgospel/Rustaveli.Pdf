using Rustaveli.Pdf.Blocks;

namespace Rustaveli.Pdf;

/// <summary>
/// Adds the cells of a table's header rows or footer rows, which are drawn again on every page the table runs on to.
/// </summary>
public sealed class TableBand
{
    private readonly List<CellBlock> _cells;

    internal TableBand(List<CellBlock> cells) => _cells = cells;

    /// <summary>
    /// Adds a cell to the band, placed in the next free slot unless it is given a row or column of its own.
    /// </summary>
    public CellFrame Cell()
    {
        CellBlock cell = new CellBlock();
        _cells.Add(cell);
        return new CellFrame(cell);
    }
}
