namespace Rustaveli.Pdf;

/// <summary>
/// A composed document, ready to be rendered.
/// </summary>
/// <remarks>
/// Despite reading like an immutable value, a document owns a mutable element tree. Rendering walks that tree
/// and records progress in it — how many lines of a paragraph have been drawn, which table rows remain — so a
/// single instance must not be rendered from two threads at once. Doing so interleaves those cursors and yields
/// silently wrong output rather than an exception.
/// Rendering the same instance repeatedly one after another is fine: each pass resets the tree before it starts.
/// </remarks>
public sealed class Document : IComposition
{
    private readonly List<Section> _pages = new List<Section>();

    /// <summary>The section each document merged into this one begins at; this document alone is one, from 0.</summary>
    private readonly List<int> _partStarts = [0];

    /// <summary>The style sheet of each document merged into this one, in order; none for a document composed alone.</summary>
    private readonly List<StyleSheet> _partStyles = [];

    private int _pageLimit = 10_000;

    public DocumentInfo Info { get; } = new DocumentInfo();

    /// <summary>The document's named type, paragraph and frame styles.</summary>
    public StyleSheet Styles { get; } = new StyleSheet();

    /// <summary>
    /// The most pages the document may take, 10,000 unless set. Content that never stops asking for another page —
    /// a frame that reports more to come but takes no room — fails once it passes this, rather than running forever.
    /// </summary>
    public int PageLimit
    {
        get => _pageLimit;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _pageLimit = value;
        }
    }

    internal IReadOnlyList<Section> Sections => _pages;

    /// <summary>Whether each merged document numbers its pages from 1 and counts only its own.</summary>
    internal bool NumbersPartsApart { get; private set; }

    internal int PartCount => _partStarts.Count;

    /// <summary>Which merged document the section at <paramref name="section"/> came from.</summary>
    internal int PartOf(int section) => _partStarts.FindLastIndex(start => start <= section);

    /// <summary>The style sheet of the merged document <paramref name="part"/>, which its content names styles from.</summary>
    internal StyleSheet StylesOf(int part) => _partStyles.Count > part ? _partStyles[part] : Styles;

    /// <summary>
    /// A document of every page of <paramref name="documents"/>, one after another, numbered on from one to the next
    /// unless <see cref="NumberPartsSeparately"/> says otherwise. It describes itself as the first does.
    /// </summary>
    public static Document Merge(params Document[] documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        if (documents.Length == 0 || documents.Any(document => document is null))
            throw new ArgumentException("Merging needs one document or more, and no null among them.", nameof(documents));

        Document merged = new Document();
        merged._partStarts.Clear();

        foreach (Document document in documents)
        {
            merged._partStarts.Add(merged._pages.Count);
            merged._partStyles.Add(document.Styles);
            merged._pages.AddRange(document._pages);
        }

        // Each document may take the pages it allows, so together they may take them all.
        merged.PageLimit = (int)Math.Min(int.MaxValue, documents.Sum(document => (long)document.PageLimit));

        DocumentInfo first = documents[0].Info;
        merged.Info.Title = first.Title;
        merged.Info.Author = first.Author;
        merged.Info.Subject = first.Subject;
        merged.Info.Keywords = first.Keywords;
        merged.Info.Creator = first.Creator;
        merged.Info.Producer = first.Producer;
        merged.Info.CreationDate = first.CreationDate;
        merged.Info.ModificationDate = first.ModificationDate;
        merged.Info.Language = first.Language;
        return merged;
    }

    /// <summary>
    /// Numbers each merged document's pages from 1, and counts only its own pages in "page 3 of 7", as each would be
    /// numbered printed alone.
    /// </summary>
    public Document NumberPartsSeparately()
    {
        NumbersPartsApart = true;
        return this;
    }

    private Document()
    {
    }

    /// <summary>
    /// Builds a document by invoking <paramref name="compose" />, which declares one or more page runs.
    /// </summary>
    public static Document Compose(Action<IComposition> compose)
    {
        ArgumentNullException.ThrowIfNull(compose);
        Document document = new Document();
        try
        {
            using (document.Styles.Use())
                compose(document);
        }
        catch (Exception ex) when (!(ex is CompositionException))
        {
            throw new CompositionException("The document could not be composed. See the inner exception for details.", ex);
        }
        return document;
    }

    void IComposition.Section(Action<Section> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Section pageDescriptor = new Section();
        handler(pageDescriptor);
        _pages.Add(pageDescriptor);
    }
}
