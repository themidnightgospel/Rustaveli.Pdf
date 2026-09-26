using CsCheck;

namespace Rustaveli.Pdf.UnitTests.PropertyBased;

/// <summary>
/// Random layout trees. Sizes are bounded so every generated tree fits a page somehow — a box never taller than a
/// page, rows never narrower than a word — which makes "the document renders" a property rather than a coin toss.
/// </summary>
internal static class TreeGenerator
{
    // Computed rather than stored: static initialisers run in declaration order, and this one depends on those below.
    public static Gen<TreeNode> Tree => Node(depth: 3, wide: true);

    private static Gen<TreeNode> Leaf { get; } = Gen.OneOf(
        Gen.Int[1, 120].Select(words => new TreeNode(NodeKind.Text, words, [], [])),
        Gen.Int[0, 90].Select(height => new TreeNode(NodeKind.Box, height, [], [])));

    /// <param name="depth">How many more levels of nesting are allowed.</param>
    /// <param name="wide">
    /// False inside a row item, which may be as narrow as its constant width: rows and tables there could need
    /// more width than exists, making the tree impossible to lay out rather than exposing a defect.
    /// </param>
    private static Gen<TreeNode> Node(int depth, bool wide)
    {
        if (depth == 0)
            return Leaf;

        Gen<TreeNode> child = Node(depth - 1, wide);
        Gen<TreeNode> column = Gen.Select(Gen.Int[0, 12], child.List[1, 6], (spacing, children) =>
            new TreeNode(NodeKind.Column, spacing, [], children));
        Gen<TreeNode> padding = Gen.Select(Gen.Int[0, 10], child, (amount, inner) => new TreeNode(NodeKind.Padding, amount, [], [inner]));
        Gen<TreeNode> background = child.Select(inner => new TreeNode(NodeKind.Background, 0, [], [inner]));
        Gen<TreeNode> border = child.Select(inner => new TreeNode(NodeKind.Border, 0, [], [inner]));

        if (!wide)
            return Gen.Frequency((3, Leaf), (3, column), (1, padding), (1, background), (1, border));

        Gen<TreeNode> narrowChild = Node(depth - 1, wide: false);
        Gen<TreeNode> row = Gen.Select(RowWidths, narrowChild.List[3, 3], (widths, children) =>
            new TreeNode(NodeKind.Row, 0, widths, children.Take(widths.Count).ToList()));
        Gen<TreeNode> table = Gen.Select(Gen.Int[1, 3], Gen.Int[1, 40].Select(words => new TreeNode(NodeKind.Text, words, [], [])).List[1, 9], (columns, cells) =>
            new TreeNode(NodeKind.Table, 0, [columns], cells));

        return Gen.Frequency((3, Leaf), (3, column), (2, row), (1, table), (1, padding), (1, background), (1, border));
    }

    // One to three items, each either relative (0) or a constant width wide enough for any generated word.
    private static Gen<IReadOnlyList<int>> RowWidths { get; } =
        Gen.OneOf(Gen.Const(0), Gen.Int[60, 110]).List[1, 3].Select(widths => (IReadOnlyList<int>)widths);
}
