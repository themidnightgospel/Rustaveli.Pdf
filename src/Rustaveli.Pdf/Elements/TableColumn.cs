using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Declares how one table column is sized.
/// </summary>
public sealed class TableColumn
{
    /// <summary>When true, <see cref="Value"/> is a weight; otherwise it is a width in points.</summary>
    public bool IsRelative { get; init; }

    public float Value { get; init; } = 1f;

    public static TableColumn Relative(float weight) => new() { IsRelative = true, Value = weight };

    public static TableColumn Constant(float width) => new() { IsRelative = false, Value = width };
}
