using Rustaveli.Pdf.Elements;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Builds one band of a table — its body, header or footer.
/// </summary>
public sealed class TableBand(List<CellBlock> cells)
{
    /// <summary>Adds a cell. Without an explicit position it is placed in the next free slot.</summary>
    public CellFrame Cell()
    {
        CellBlock cell = new CellBlock();
        cells.Add(cell);
        return new CellFrame(cell);
    }
}
