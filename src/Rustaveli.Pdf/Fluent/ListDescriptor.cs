using Rustaveli.Pdf.Elements;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Builds the contents of a list.
/// </summary>
public sealed class ListDescriptor(ListBlock element)
{
    /// <summary>Marks items with a bullet. This is the default.</summary>
    public void Unordered() => element.Marker = ListNumbering.Bullet;

    /// <summary>Numbers the items, using arabic numerals unless another style is given.</summary>
    public void Ordered(ListNumbering marker = ListNumbering.Decimal) => element.Marker = marker;

    /// <summary>Sets the width of the gutter the markers sit in.</summary>
    public void MarkerWidth(float width) => element.MarkerWidth = width;

    /// <summary>Adjusts the style of the markers, leaving the item content untouched.</summary>
    public void MarkerStyle(Func<TypeStyle, TypeStyle> refinement) => element.MarkerStyle = refinement;

    /// <summary>Sets the vertical gap between items.</summary>
    public void Spacing(float value) => element.Spacing = value;

    /// <summary>Adds an item and returns its container.</summary>
    public IFrame Item()
    {
        ListEntry item = new ListEntry();
        element.Items.Add(item);
        return item;
    }
}
