namespace Rustaveli.Pdf.UnitTests.PropertyBased;

/// <summary>
/// One node of a generated layout tree: plain data, so a failing case prints in full and can be rebuilt exactly.
/// </summary>
/// <param name="Kind">What the node composes.</param>
/// <param name="Amount">Kind-specific: word count for text, height for a box, spacing for a column, padding.</param>
/// <param name="Sizes">For a row, each item's width (positive: constant, zero: relative); for a table, its column count.</param>
/// <param name="Children">Nested nodes.</param>
public sealed record TreeNode(NodeKind Kind, int Amount, IReadOnlyList<int> Sizes, IReadOnlyList<TreeNode> Children)
{
    public override string ToString() => Kind switch
    {
        NodeKind.Text => $"Text({Amount})",
        NodeKind.Box => $"Box({Amount})",
        NodeKind.Row => $"Row[{string.Join(",", Sizes)}]({string.Join(", ", Children)})",
        NodeKind.Table => $"Table{Sizes[0]}({string.Join(", ", Children)})",
        _ => $"{Kind}({Amount})({string.Join(", ", Children)})"
    };
}
