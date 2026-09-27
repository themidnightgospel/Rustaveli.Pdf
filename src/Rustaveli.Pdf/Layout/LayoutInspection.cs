using Rustaveli.Pdf.Blocks;

namespace Rustaveli.Pdf.Layout;

/// <summary>
/// Every frame as it was drawn, page by page, each nested within the one that drew it: where on the page it lies, how
/// much room it had, and the code that made it — what an inspector shows.
/// </summary>
internal sealed class LayoutInspection
{
    private readonly List<List<Node>> _pages = [];
    private Node? _current;

    /// <summary>The frames drawn on each page, outermost first.</summary>
    public IReadOnlyList<IReadOnlyList<Node>> Pages => _pages;

    /// <summary>Begins recording a new page.</summary>
    public void BeginPage()
    {
        _pages.Add([]);
        _current = null;
    }

    public Node Enter(Block block, Offset origin, Extent size)
    {
        Node node = new Node(Name(block), block.Source, origin, size, _current);

        if (_current is not null)
            _current.Children.Add(node);
        else if (_pages.Count > 0)
            _pages[^1].Add(node);

        _current = node;
        return node;
    }

    public void Leave(Node node) => _current = node.Parent;

    /// <summary>A frame as the inspector names it: by its name, or by what it is.</summary>
    internal static string Name(Block block)
    {
        if (block is LabelBlock labelled)
            return "\"" + labelled.Label + "\"";

        string name = block.GetType().Name;
        return name.EndsWith("Block", StringComparison.Ordinal) ? name.Substring(0, name.Length - "Block".Length) : name;
    }

    /// <summary>One frame drawn: its name, where it came from, its top left on the page and its room.</summary>
    internal sealed class Node(string name, string? source, Offset origin, Extent size, Node? parent)
    {
        public string Name { get; } = name;

        public string? Source { get; } = source;

        public Offset Origin { get; } = origin;

        public Extent Size { get; } = size;

        public Node? Parent { get; } = parent;

        public List<Node> Children { get; } = [];
    }
}
