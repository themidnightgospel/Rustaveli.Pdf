using System.Globalization;
using System.Text;

namespace Rustaveli.Pdf.Layout;

/// <summary>
/// The measurements a layout made, each nested within the one that asked for it, kept to explain a failure: which
/// frame, inside which, was offered what room and could not fit in it.
/// </summary>
internal sealed class PlanTrace
{
    private readonly List<Node> _roots = [];
    private Node? _current;

    public IReadOnlyList<Node> Roots => _roots;

    /// <summary>Begins recording a measurement of <paramref name="block"/> in <paramref name="space"/>.</summary>
    public Node Enter(Block block, Extent space)
    {
        Node node = new Node(block, space, _current);
        (_current?.Children ?? _roots).Add(node);
        _current = node;
        return node;
    }

    /// <summary>Ends recording <paramref name="node"/>, returning to the measurement that asked for it.</summary>
    public void Leave(Node node) => _current = node.Parent;

    /// <summary>
    /// The frames that could not fit, from the outermost down to the one that refused the room first: at each level,
    /// the measurement that led the one above it to give up.
    /// </summary>
    public string Describe()
    {
        StringBuilder text = new StringBuilder();
        Node? node = _roots.LastOrDefault(root => root.Result is { IsDeferred: true });

        for (int depth = 1; node is not null;)
        {
            Node? next = node.Children.LastOrDefault(child => child.Result is { IsDeferred: true });

            // Frames are the slots content is composed into, not content: they are passed over on the way down.
            if (node.Block is Frame && next is not null)
            {
                node = next;
                continue;
            }

            text.Append('\n').Append(' ', depth++ * 2).Append(Name(node.Block)).Append(", offered ").Append(Format(node.Space)).Append(": ");

            // Frames above give up because the one below did; only the last says why.
            text.Append("does not fit");

            if (next is null && node.Result!.Value.DeferReason is { Length: > 0 } reason)
                text.Append(" — ").Append(reason);

            node = next;
        }

        return text.Length == 0 ? string.Empty : "\nWhere it did not fit, from the page down:" + text;
    }

    /// <summary>A frame as the trace names it: by its label, or by what it is.</summary>
    private static string Name(Block block) => LayoutInspection.Name(block);

    private static string Format(Extent size) => Length(size.Width) + " × " + Length(size.Height);

    private static string Length(float length) =>
        length >= Extent.Max.Height ? "unbounded" : length.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>One measurement: what was measured, in what room, what it reported, and what it measured in turn.</summary>
    internal sealed class Node(Block block, Extent space, Node? parent)
    {
        public Block Block { get; } = block;

        public Extent Space { get; } = space;

        public Node? Parent { get; } = parent;

        public Fit? Result { get; set; }

        public List<Node> Children { get; } = [];
    }
}
