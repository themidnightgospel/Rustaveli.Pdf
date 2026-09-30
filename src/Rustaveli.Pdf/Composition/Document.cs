namespace Rustaveli.Pdf;

/// <summary>
/// A composed document, ready to be rendered.
/// </summary>
/// <remarks>
/// A document owns the element tree its composing built, and laying it out records progress in that tree — how many
/// lines of a paragraph have been drawn, which table rows remain. An export uses the tree when no other export is,
/// and each resets it before it starts. An export that begins while another is under way composes the document
/// afresh for itself, running the composing code again, so one document may be exported from any number of threads
/// at once.
/// </remarks>
public sealed class Document : IComposition
{
    private readonly List<Section> _pages = new List<Section>();

    /// <summary>The section each document merged into this one begins at; this document alone is one, from 0.</summary>
    private readonly List<int> _partStarts = [0];

    /// <summary>The style sheet of each document merged into this one, in order; none for a document composed alone.</summary>
    private readonly List<StyleSheet> _partStyles = [];

    /// <summary>Composes this document again from scratch: its composing code run anew, or its parts merged anew.</summary>
    private readonly Func<Document> _recompose;

    private int _pageLimit = 10_000;

    /// <summary>1 while an export is laying out this document's own tree; 0 otherwise.</summary>
    private int _exporting;

    private Document(Func<Document> recompose) => _recompose = recompose;

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

        Document[] parts = documents.ToArray();
        return MergeOf(parts);
    }

    /// <summary>
    /// Merges fresh copies of <paramref name="parts"/>, so that exporting the merged document never lays out a tree
    /// an export of one of its parts is using.
    /// </summary>
    private static Document MergeOf(Document[] parts)
    {
        Document merged = new Document(() => MergeOf(parts));
        merged._partStarts.Clear();

        foreach (Document part in parts)
        {
            // A part that is itself a merge brings its own parts, each with its boundary and style sheet, rather than
            // becoming one part whose sheet — the merge's own, which its content never named styles from — is empty.
            // A fresh copy always carries its style sheets, one for a document composed alone.
            Document fresh = part.Recompose();
            int offset = merged._pages.Count;
            merged._partStarts.AddRange(fresh._partStarts.Select(start => offset + start));
            merged._partStyles.AddRange(fresh._partStyles);
            merged._pages.AddRange(fresh._pages);
        }

        // Each document may take the pages it allows, so together they may take them all.
        merged.PageLimit = (int)Math.Min(int.MaxValue, parts.Sum(part => (long)part.PageLimit));
        CopyInfo(parts[0].Info, merged.Info);
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

    /// <summary>
    /// Builds a document by invoking <paramref name="compose" />, which declares one or more page runs. It is invoked
    /// again for an export that begins while another is laying the document out.
    /// </summary>
    public static Document Compose(Action<IComposition> compose)
    {
        ArgumentNullException.ThrowIfNull(compose);
        return ComposeWith(compose);
    }

    /// <summary>
    /// This document, for one export to lay out — or, while another export is laying it out, a copy composed afresh.
    /// Disposing the lease hands the document back.
    /// </summary>
    internal ExportLease ForExport() =>
        Interlocked.CompareExchange(ref _exporting, 1, 0) == 0
            ? new ExportLease(this, this)
            : new ExportLease(Recompose(), null);

    /// <summary>
    /// A copy of this document composed afresh, with the settings made on this one since: its information, page
    /// limit and numbering, and the style sheets its content names styles from.
    /// </summary>
    internal Document Recompose()
    {
        Document fresh = _recompose();
        fresh.PageLimit = PageLimit;
        fresh.NumbersPartsApart = NumbersPartsApart;
        CopyInfo(Info, fresh.Info);

        // Styles are only read while a document is laid out, so the copy reads this document's own sheets — those
        // defined after composing among them.
        fresh._partStyles.Clear();
        fresh._partStyles.AddRange(_partStyles.Count > 0 ? _partStyles : [Styles]);
        return fresh;
    }

    private static void CopyInfo(DocumentInfo from, DocumentInfo to)
    {
        to.Title = from.Title;
        to.Author = from.Author;
        to.Subject = from.Subject;
        to.Keywords = from.Keywords;
        to.Creator = from.Creator;
        to.Producer = from.Producer;
        to.CreationDate = from.CreationDate;
        to.ModificationDate = from.ModificationDate;
        to.Language = from.Language;
    }

    private static Document ComposeWith(Action<IComposition> compose)
    {
        Document document = new Document(() => ComposeWith(compose));
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

    void IComposition.Section(Action<Section> handler) => throw new NotImplementedException("To be written anew from its specification.");

    /// <summary>The document one export lays out; disposing it hands a document's own tree back for the next.</summary>
    internal readonly struct ExportLease(Document document, Document? held) : IDisposable
    {
        public Document Document { get; } = document;

        public void Dispose()
        {
            if (held is not null)
                Volatile.Write(ref held._exporting, 0);
        }
    }
}
