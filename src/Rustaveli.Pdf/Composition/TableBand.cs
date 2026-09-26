using Rustaveli.Pdf.Blocks;

namespace Rustaveli.Pdf;

/// <summary>
/// Builds one band of a table — its body, header or footer.
/// </summary>
public sealed class TableBand
{
    private readonly List<CellBlock> _cells;

    internal TableBand(List<CellBlock> cells) => _cells = cells;

    /// <summary>Adds a cell. Without an explicit position it is placed in the next free slot.</summary>
    public CellFrame Cell()
    {
        CellBlock cell = new CellBlock();
        _cells.Add(cell);
        return new CellFrame(cell);
    }
}
