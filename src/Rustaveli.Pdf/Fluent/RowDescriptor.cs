using Rustaveli.Pdf.Elements;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Builds the contents of a horizontal stack.
/// </summary>
public sealed class RowDescriptor(RowElement element)
{
    /// <summary>Sets the gap inserted between consecutive items.</summary>
    public void Spacing(float value) => element.Spacing = value;

    /// <summary>Adds an item that shares leftover width with other relative items, proportional to its weight.</summary>
    public IFrame RelativeItem(float weight = 1f) => Add(RowItemSizing.Relative, weight);

    /// <summary>Adds an item of fixed width.</summary>
    public IFrame ConstantItem(float width) => Add(RowItemSizing.Constant, width);

    /// <summary>Adds an item that takes exactly as much width as its content needs.</summary>
    public IFrame AutoItem() => Add(RowItemSizing.Auto, 0f);

    private IFrame Add(RowItemSizing sizing, float value)
    {
        RowItem item = new RowItem { Sizing = sizing, Value = value };
        element.Items.Add(item);
        return item;
    }
}
