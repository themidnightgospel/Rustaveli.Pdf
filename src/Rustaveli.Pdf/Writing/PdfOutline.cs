namespace Rustaveli.Pdf.Writing;

/// <summary>
/// Writes a document outline — the bookmarks a viewer lists beside the pages — from entries in document order, each
/// nested under the nearest entry before it of a shallower level.
/// </summary>
/// <remarks>
/// Every entry is written open, its whole subtree shown, as the outline of a document is read top to bottom.
/// </remarks>
internal static class PdfOutline
{
    private static readonly PdfName Next = new PdfName("Next");
    private static readonly PdfName Last = new PdfName("Last");

    /// <summary>The outline's root, written with every entry beneath it.</summary>
    public static PdfReference Write(PdfFileWriter file, IReadOnlyList<(string Title, int Level, PdfArray Destination)> entries)
    {
        Node root = new Node(string.Empty, 0, null, file.Reserve());
        List<Node> open = [root];

        foreach ((string title, int level, PdfArray destination) in entries)
        {
            while (open[open.Count - 1].Level >= level)
                open.RemoveAt(open.Count - 1);

            Node node = new Node(title, level, destination, file.Reserve());
            open[open.Count - 1].Children.Add(node);
            open.Add(node);
        }

        WriteChildren(file, root);

        PdfDictionary outlines = new PdfDictionary
        {
            [PdfNames.Type] = PdfNames.Outlines,
            [PdfNames.Count] = root.Descendants,
        };

        Link(outlines, root);
        file.Write(root.Reference, outlines);

        return root.Reference;
    }

    private static void WriteChildren(PdfFileWriter file, Node parent)
    {
        for (int index = 0; index < parent.Children.Count; index++)
        {
            Node node = parent.Children[index];
            PdfDictionary item = new PdfDictionary
            {
                [PdfNames.Title] = PdfString.FromText(node.Title),
                [PdfNames.Parent] = parent.Reference,
                [PdfNames.Dest] = node.Destination!,
            };

            if (index > 0)
                item[PdfNames.Prev] = parent.Children[index - 1].Reference;

            if (index < parent.Children.Count - 1)
                item[Next] = parent.Children[index + 1].Reference;

            if (node.Children.Count > 0)
            {
                Link(item, node);
                item[PdfNames.Count] = node.Descendants;
            }

            WriteChildren(file, node);
            file.Write(node.Reference, item);
        }
    }

    private static void Link(PdfDictionary item, Node node)
    {
        if (node.Children.Count == 0)
            return;

        item[PdfNames.First] = node.Children[0].Reference;
        item[Last] = node.Children[node.Children.Count - 1].Reference;
    }

    private sealed class Node(string title, int level, PdfArray? destination, PdfReference reference)
    {
        public string Title { get; } = title;

        public int Level { get; } = level;

        public PdfArray? Destination { get; } = destination;

        public PdfReference Reference { get; } = reference;

        public List<Node> Children { get; } = [];

        public int Descendants => Children.Sum(child => 1 + child.Descendants);
    }
}
