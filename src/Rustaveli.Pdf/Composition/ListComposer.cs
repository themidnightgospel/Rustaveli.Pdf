using Rustaveli.Pdf.Blocks;

namespace Rustaveli.Pdf;

/// <summary>
/// Builds the contents of a list.
/// </summary>
public sealed class ListComposer
{
    private readonly ListBlock _block;

    internal ListComposer(ListBlock block) => _block = block;

    /// <summary>Marks items with a bullet. This is the default.</summary>
    public void Bulleted() => _block.Marker = ListNumbering.Bullet;

    /// <summary>Numbers the items, using arabic numerals unless another style is given.</summary>
    public void Numbered(ListNumbering marker = ListNumbering.Arabic) => _block.Marker = marker;

    /// <summary>Sets the width of the gutter the markers sit in.</summary>
    public void MarkerIndent(float width) => _block.MarkerWidth = width;

    /// <summary>Adjusts the style of the markers, leaving the item content untouched.</summary>
    public void MarkerType(Func<TypeStyle, TypeStyle> refinement) => _block.MarkerStyle = refinement;

    /// <summary>Sets the vertical gap between items.</summary>
    public void SpaceBetween(float value) => _block.Spacing = value;

    /// <summary>Adds an item and returns its container.</summary>
    public IFrame Add()
    {
        ListEntry item = new ListEntry();
        _block.Items.Add(item);
        return item;
    }
}
