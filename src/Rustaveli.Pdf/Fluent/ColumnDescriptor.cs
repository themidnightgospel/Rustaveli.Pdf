using Rustaveli.Pdf.Elements;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Builds the contents of a vertical stack.
/// </summary>
public sealed class ColumnDescriptor(StackBlock element)
{
    /// <summary>Sets the gap inserted between consecutive items.</summary>
    public void Spacing(float value)
    {
        element.Spacing = value;
    }

    /// <summary>Adds an item to the bottom of the stack and returns its container.</summary>
    public IFrame Item()
    {
        Frame container = new Frame();
        element.Items.Add(container);
        return container;
    }
}
