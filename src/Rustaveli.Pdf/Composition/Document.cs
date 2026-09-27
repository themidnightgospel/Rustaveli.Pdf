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
