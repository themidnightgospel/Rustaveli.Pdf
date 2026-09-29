using Rustaveli.Pdf.Blocks;

namespace Rustaveli.Pdf;

/// <summary>
/// Builds the contents of a horizontal stack.
/// </summary>
public sealed class ColumnsComposer
{
    private readonly ColumnsBlock _block;

    internal ColumnsComposer(ColumnsBlock block) => _block = block;

    /// <summary>Sets the gap inserted between consecutive items.</summary>
    public void Gutter(float value) => _block.Gutter = Numbers.NotNegative(value, nameof(value));

    /// <summary>Adds an item that shares leftover width with other relative items, proportional to its weight.</summary>
    public IFrame Share(float weight = 1f) => Add(ColumnSizing.Share, Numbers.NotNegative(weight, nameof(weight)));

    /// <summary>Adds an item of fixed width.</summary>
    public IFrame Fixed(float width) => Add(ColumnSizing.Fixed, Numbers.NotNegative(width, nameof(width)));

    /// <summary>Adds an item that takes exactly as much width as its content needs.</summary>
    public IFrame Natural() => Add(ColumnSizing.Natural, 0f);

    private IFrame Add(ColumnSizing sizing, float value)
    {
        ColumnSlot item = new ColumnSlot { Sizing = sizing, Value = value };
        _block.Items.Add(item);
        return item;
    }
}
