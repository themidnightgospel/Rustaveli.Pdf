using Rustaveli.Pdf.Blocks;

namespace Rustaveli.Pdf;

/// <summary>
/// Adds the items of a row set side by side, each in a frame of its own, in the order they read.
/// </summary>
/// <remarks>
/// An item is as wide as it is told to be (<see cref="Fixed"/>), as wide as its content (<see cref="Natural"/>), or a
/// share of whatever width those leave (<see cref="Share"/>). Items that run on to another page keep their place in
/// the row there.
/// </remarks>
public sealed class ColumnsComposer
{
    private readonly ColumnsBlock _row;

    internal ColumnsComposer(ColumnsBlock row) => _row = row;

    /// <summary>Sets the room left between two neighbouring items, in points; none unless set.</summary>
    public void Gutter(float value) => _row.Gutter = Numbers.NotNegative(value, nameof(value));

    /// <summary>
    /// Adds an item taking a share of the width left once fixed and natural items have theirs, in proportion to its
    /// weight among the items that share. A weight of zero takes none.
    /// </summary>
    public IFrame Share(float weight = 1f) => Add(ColumnSizing.Share, Numbers.NotNegative(weight, nameof(weight)));

    /// <summary>Adds an item exactly <paramref name="width"/> points wide.</summary>
    public IFrame Fixed(float width) => Add(ColumnSizing.Fixed, Numbers.NotNegative(width, nameof(width)));

    /// <summary>Adds an item as wide as its content needs, and no wider than the row has room for.</summary>
    public IFrame Natural() => Add(ColumnSizing.Natural, 0f);

    private IFrame Add(ColumnSizing sizing, float value)
    {
        ColumnSlot item = new ColumnSlot { Sizing = sizing, Value = value };
        _row.Items.Add(item);
        return item;
    }
}
