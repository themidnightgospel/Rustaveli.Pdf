using Rustaveli.Pdf.Elements;
using Rustaveli.Pdf.Exceptions;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Builds one band of a table — its body, header or footer.
/// </summary>
public sealed class TableBandDescriptor(List<TableCell> cells)
{
    /// <summary>Adds a cell. Without an explicit position it is placed in the next free slot.</summary>
    public TableCellDescriptor Cell()
    {
        TableCell cell = new TableCell();
        cells.Add(cell);
        return new TableCellDescriptor(cell);
    }
}
