using Rustaveli.Pdf.Elements;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Declares the columns of a table.
/// </summary>
public sealed class TableColumns(TableElement element)
{
    /// <summary>Adds a column that shares leftover width with other relative columns, proportional to its weight.</summary>
    public void Share(float weight = 1f) => element.Columns.Add(TableColumnSpec.Relative(weight));

    /// <summary>Adds a column of fixed width in points.</summary>
    public void Fixed(float width) => element.Columns.Add(TableColumnSpec.Constant(width));
}
