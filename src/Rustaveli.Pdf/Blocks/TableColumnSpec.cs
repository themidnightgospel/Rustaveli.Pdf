namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Declares how one table column is sized.
/// </summary>
internal sealed class TableColumnSpec
{
    /// <summary>When true, <see cref="Value"/> is a weight; otherwise it is a width in points.</summary>
    public bool TakesShare { get; init; }

    public float Value { get; init; } = 1f;

    public static TableColumnSpec Share(float weight) => new() { TakesShare = true, Value = weight };

    public static TableColumnSpec Fixed(float width) => new() { TakesShare = false, Value = width };
}
