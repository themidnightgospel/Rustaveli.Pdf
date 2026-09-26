using Rustaveli.Pdf.Elements;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Builds the contents of a horizontal stack.
/// </summary>
public sealed class ColumnsComposer(ColumnsBlock element)
{
    /// <summary>Sets the gap inserted between consecutive items.</summary>
    public void Gutter(float value) => element.Spacing = value;

    /// <summary>Adds an item that shares leftover width with other relative items, proportional to its weight.</summary>
    public IFrame Share(float weight = 1f) => Add(ColumnSizing.Relative, weight);

    /// <summary>Adds an item of fixed width.</summary>
    public IFrame Fixed(float width) => Add(ColumnSizing.Constant, width);

    /// <summary>Adds an item that takes exactly as much width as its content needs.</summary>
    public IFrame Natural() => Add(ColumnSizing.Auto, 0f);

    private IFrame Add(ColumnSizing sizing, float value)
    {
        ColumnSlot item = new ColumnSlot { Sizing = sizing, Value = value };
        element.Items.Add(item);
        return item;
    }
}
