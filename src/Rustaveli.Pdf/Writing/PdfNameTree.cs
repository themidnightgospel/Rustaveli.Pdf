namespace Rustaveli.Pdf.Writing;

/// <summary>
/// Builds a name tree (ISO 32000-1, 7.9.6): string keys mapped to values, sorted, and split into balanced nodes so
/// a reader can find a key by descending through <c>/Limits</c> instead of scanning every entry.
/// </summary>
internal sealed class PdfNameTree
{
    /// <summary>The most key–value pairs in a leaf, and the most kids under an intermediate node.</summary>
    public const int MaxEntries = 32;

    private readonly List<KeyValuePair<PdfString, PdfValue>> _entries = new List<KeyValuePair<PdfString, PdfValue>>();

    public int Count => _entries.Count;

    public void Add(PdfString key, PdfValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        _entries.Add(new KeyValuePair<PdfString, PdfValue>(key, value));
    }

    /// <summary>Writes the tree's nodes and returns the root. Keys must be distinct.</summary>
    public PdfReference Write(PdfFileWriter writer)
    {
        // Readers binary-search the keys, and the specification orders them by their bytes, not as text.
        List<KeyValuePair<PdfString, PdfValue>> sorted = new List<KeyValuePair<PdfString, PdfValue>>(_entries);
        sorted.Sort((left, right) => left.Key.Bytes.SequenceCompareTo(right.Key.Bytes));
        for (int index = 1; index < sorted.Count; index++)
        {
            if (sorted[index - 1].Key.Bytes.SequenceEqual(sorted[index].Key.Bytes))
                throw new InvalidOperationException("A name tree cannot hold the same key twice.");
        }

        if (sorted.Count <= MaxEntries)
            return writer.Write(new PdfDictionary { [PdfNames.Names] = Pairs(sorted, 0, sorted.Count) });

        List<Node> level = new List<Node>();
        for (int start = 0; start < sorted.Count; start += MaxEntries)
        {
            int end = Math.Min(start + MaxEntries, sorted.Count);
            PdfString first = sorted[start].Key;
            PdfString last = sorted[end - 1].Key;
            PdfReference leaf = writer.Write(new PdfDictionary
            {
                [PdfNames.Limits] = new PdfArray { first, last },
                [PdfNames.Names] = Pairs(sorted, start, end),
            });

            level.Add(new Node(leaf, first, last));
        }

        while (level.Count > MaxEntries)
        {
            List<Node> parents = new List<Node>();
            for (int start = 0; start < level.Count; start += MaxEntries)
            {
                int end = Math.Min(start + MaxEntries, level.Count);
                PdfString first = level[start].First;
                PdfString last = level[end - 1].Last;
                PdfReference parent = writer.Write(new PdfDictionary
                {
                    [PdfNames.Limits] = new PdfArray { first, last },
                    [PdfNames.Kids] = Kids(level, start, end),
                });

                parents.Add(new Node(parent, first, last));
            }

            level = parents;
        }

        // The root carries no /Limits: it spans everything by definition.
        return writer.Write(new PdfDictionary { [PdfNames.Kids] = Kids(level, 0, level.Count) });
    }

    private static PdfArray Pairs(List<KeyValuePair<PdfString, PdfValue>> entries, int start, int end)
    {
        PdfArray pairs = new PdfArray(2 * (end - start));
        for (int index = start; index < end; index++)
        {
            pairs.Add(entries[index].Key);
            pairs.Add(entries[index].Value);
        }

        return pairs;
    }

    private static PdfArray Kids(List<Node> nodes, int start, int end)
    {
        PdfArray kids = new PdfArray(end - start);
        for (int index = start; index < end; index++)
            kids.Add(nodes[index].Reference);

        return kids;
    }

    private readonly struct Node(PdfReference reference, PdfString first, PdfString last)
    {
        public PdfReference Reference { get; } = reference;

        public PdfString First { get; } = first;

        public PdfString Last { get; } = last;
    }
}
