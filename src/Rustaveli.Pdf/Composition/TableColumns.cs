using Rustaveli.Pdf.Blocks;

namespace Rustaveli.Pdf;

/// <summary>
/// Declares the columns of a table.
/// </summary>
public sealed class TableColumns
{
    private readonly TableBlock _block;

    internal TableColumns(TableBlock block) => _block = block;

    /// <summary>Adds a column that takes a share of the width the fixed columns leave, in proportion to its weight.</summary>
    public void Share(float weight = 1f) => _block.Columns.Add(TableColumnSpec.Share(Numbers.NotNegative(weight, nameof(weight))));

    /// <summary>Adds a column of fixed width in points.</summary>
    public void Fixed(float width) => _block.Columns.Add(TableColumnSpec.Fixed(Numbers.NotNegative(width, nameof(width))));
}
