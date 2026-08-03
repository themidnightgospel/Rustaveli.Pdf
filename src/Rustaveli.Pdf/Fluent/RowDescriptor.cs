using Rustaveli.Pdf.Elements;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Builds the contents of a horizontal stack.
/// </summary>
public sealed class RowDescriptor(RowElement element)
{
    /// <summary>Sets the gap inserted between consecutive items.</summary>
    public void Spacing(float value) => element.Spacing = value;

    /// <summary>Adds an item that shares leftover width with other relative items, proportional to its weight.</summary>
    public IContainer RelativeItem(float weight = 1f) => Add(RowItemSizing.Relative, weight);

    /// <summary>Adds an item of fixed width.</summary>
    public IContainer ConstantItem(float width) => Add(RowItemSizing.Constant, width);

    /// <summary>Adds an item that takes exactly as much width as its content needs.</summary>
    public IContainer AutoItem() => Add(RowItemSizing.Auto, 0f);

    private IContainer Add(RowItemSizing sizing, float value)
    {
        RowItem item = new RowItem { Sizing = sizing, Value = value };
        element.Items.Add(item);
        return item;
    }
}
