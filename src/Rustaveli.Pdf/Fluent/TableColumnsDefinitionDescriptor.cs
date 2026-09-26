using Rustaveli.Pdf.Elements;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Declares the columns of a table.
/// </summary>
public sealed class TableColumnsDefinitionDescriptor(TableElement element)
{
    /// <summary>Adds a column that shares leftover width with other relative columns, proportional to its weight.</summary>
    public void RelativeColumn(float weight = 1f) => element.Columns.Add(TableColumn.Relative(weight));

    /// <summary>Adds a column of fixed width in points.</summary>
    public void ConstantColumn(float width) => element.Columns.Add(TableColumn.Constant(width));
}
