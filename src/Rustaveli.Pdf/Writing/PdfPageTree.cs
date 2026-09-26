namespace Rustaveli.Pdf.Writing;

/// <summary>
/// Builds a balanced page tree (ISO 32000-1, 7.7.3.2) while pages are still being written.
/// </summary>
/// <remarks>
/// <para>
/// A flat <c>/Kids</c> array of ten thousand pages makes every viewer scan it linearly to find page 5 000. Here,
/// pages fill leaf nodes of at most <see cref="MaxKids"/>, and leaves are grouped under parents of at most
/// <see cref="MaxKids"/> until one root remains, so every leaf sits at the same depth — log₃₂ of the page count.
/// </para>
/// <para>
/// A page needs its parent's number the moment it is written, so each leaf's number is reserved when its first
/// page arrives. The levels above depend on the final page count, so the nodes themselves — small objects, a few
/// hundred for a very long document — are held until <see cref="Write"/>.
/// </para>
/// </remarks>
internal sealed class PdfPageTree(PdfFileWriter writer)
{
    public const int MaxKids = 32;

    private readonly List<Node> _leaves = new List<Node>();

    public int Count { get; private set; }

    /// <summary>Appends <paramref name="page"/> as the next page and returns the node to name as its <c>/Parent</c>.</summary>
    public PdfReference Add(PdfReference page)
    {
        if (Count % MaxKids == 0)
            _leaves.Add(new Node(writer.Reserve()));

        Node leaf = _leaves[_leaves.Count - 1];
        leaf.Kids.Add(page);
        leaf.PageCount++;
        Count++;
        return leaf.Reference;
    }

    /// <summary>Writes every node of the tree and returns the root, which the catalog names as <c>/Pages</c>.</summary>
    public PdfReference Write()
    {
        // An empty page tree is well-formed syntax, but qpdf reports it as an error and viewers refuse to open it.
        if (_leaves.Count == 0)
            throw new InvalidOperationException("A document needs at least one page.");

        List<Node> level = _leaves;
        while (level.Count > 1)
        {
            List<Node> parents = new List<Node>((level.Count + MaxKids - 1) / MaxKids);
            for (int start = 0; start < level.Count; start += MaxKids)
            {
                Node parent = new Node(writer.Reserve());
                for (int index = start; index < Math.Min(start + MaxKids, level.Count); index++)
                {
                    parent.Kids.Add(level[index].Reference);
                    parent.PageCount += level[index].PageCount;
                    writer.Write(level[index].Reference, Dictionary(level[index], parent.Reference));
                }

                parents.Add(parent);
            }

            level = parents;
        }

        Node root = level[0];
        writer.Write(root.Reference, Dictionary(root, parent: null));
        return root.Reference;
    }

    private static PdfDictionary Dictionary(Node node, PdfReference? parent)
    {
        PdfArray kids = new PdfArray(node.Kids.Count);
        foreach (PdfReference kid in node.Kids)
            kids.Add(kid);

        PdfDictionary dictionary = new PdfDictionary(4) { [PdfNames.Type] = PdfNames.Pages };
        if (parent is PdfReference parentReference)
            dictionary[PdfNames.Parent] = parentReference;

        dictionary[PdfNames.Kids] = kids;
        dictionary[PdfNames.Count] = node.PageCount;
        return dictionary;
    }

    private sealed class Node(PdfReference reference)
    {
        public PdfReference Reference { get; } = reference;

        public List<PdfReference> Kids { get; } = new List<PdfReference>();

        /// <summary>Pages beneath this node: the <c>/Count</c> entry.</summary>
        public int PageCount { get; set; }
    }
}
