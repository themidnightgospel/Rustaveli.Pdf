namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Declares how one table column is sized.
/// </summary>
internal sealed class TableColumnSpec
{
    /// <summary>When true, <see cref="Value"/> is a weight; otherwise it is a width in points.</summary>
    public bool IsRelative { get; init; }

    public float Value { get; init; } = 1f;

    public static TableColumnSpec Relative(float weight) => new() { IsRelative = true, Value = weight };

    public static TableColumnSpec Constant(float width) => new() { IsRelative = false, Value = width };
}
