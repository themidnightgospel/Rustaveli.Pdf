namespace Rustaveli.Pdf.Writing;

/// <summary>
/// Writes a PDF document's logical structure — pages, the page tree, the catalog, named destinations and the
/// information dictionary — on top of a <see cref="PdfFileWriter"/>.
/// </summary>
/// <remarks>
/// <para>
/// A page is begun, drawn through its <see cref="PdfPage.Content"/> builder, and ended; ending it writes its content
/// stream and page object at once, so a long document never holds more than the pages currently open. Pages appear
/// in the order they were begun.
/// </para>
/// <para>
/// Fonts, images and anything else a page refers to are written through <see cref="File"/>. A resource whose content
/// is known only at the end — a font subset — is reserved with <see cref="PdfFileWriter.Reserve"/>, named in page
/// resources by that reference, and written before <see cref="Finish"/>.
/// </para>
/// <para>Not thread-safe: one writer produces one document from one thread at a time.</para>
/// </remarks>
internal sealed class PdfDocumentWriter : IDisposable
{
    private readonly PdfPageTree _pages;
    private readonly HashSet<PdfPage> _openPages = new HashSet<PdfPage>();
    private readonly Dictionary<string, PdfArray> _destinations =
        new Dictionary<string, PdfArray>(StringComparer.Ordinal);

    private readonly Dictionary<(long Fill, long Stroke), PdfReference> _opacityStates =
        new Dictionary<(long, long), PdfReference>();

    private bool _finished;

    public PdfDocumentWriter(Stream output, PdfWriterOptions? options = null)
    {
        File = new PdfFileWriter(output, options);
        _pages = new PdfPageTree(File);
    }

    /// <summary>The underlying file, for writing fonts, images and other objects pages refer to.</summary>
    public PdfFileWriter File { get; }

    public PdfDocumentInfo Info { get; } = new PdfDocumentInfo();

    /// <summary>
    /// Further entries for the catalog beyond those the writer supplies: <c>/Type</c>, <c>/Pages</c> and
    /// <c>/Names</c>.
    /// </summary>
    public PdfDictionary Catalog { get; } = new PdfDictionary();

    public int PageCount => _pages.Count;

    public PdfPage BeginPage(double width, double height) => BeginPage(new PdfRectangle(0, 0, width, height));

    public PdfPage BeginPage(PdfRectangle mediaBox)
    {
        ThrowIfFinished();
        PdfReference reference = File.Reserve();
        PdfReference parent = _pages.Add(reference);
        PdfPage page = new PdfPage(File, reference, parent, _pages.Count - 1, mediaBox);
        _openPages.Add(page);
        return page;
    }

    /// <summary>Writes the page's content stream and page object. The page cannot be used afterwards.</summary>
    public void EndPage(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        ThrowIfFinished();
        if (!_openPages.Contains(page))
            throw new InvalidOperationException("The page is not open in this document.");

        page.Content.EnsureComplete();
        CheckNotOwned(
            page.Entries,
            "page",
            PdfNames.Type,
            PdfNames.Parent,
            PdfNames.MediaBox,
            PdfNames.Resources,
            PdfNames.Contents,
            PdfNames.Annots);

        PdfDictionary dictionary = new PdfDictionary(6 + page.Entries.Count)
        {
            [PdfNames.Type] = PdfNames.Page,
            [PdfNames.Parent] = page.Parent,
            [PdfNames.MediaBox] = page.MediaBox.ToArray(),
            [PdfNames.Resources] = page.Resources.ToDictionary(),
        };

        // A page with nothing drawn needs no content stream at all; an absent /Contents is a blank page.
        if (page.Content.Length > 0)
            dictionary[PdfNames.Contents] = File.WriteStream(new PdfDictionary(), page.Content.Content);

        if (page.Annotations.Count > 0)
        {
            PdfArray annotations = new PdfArray(page.Annotations.Count);
            foreach (PdfReference annotation in page.Annotations)
                annotations.Add(annotation);

            dictionary[PdfNames.Annots] = annotations;
        }

        foreach (KeyValuePair<PdfName, PdfValue> entry in page.Entries)
            dictionary.Add(entry.Key, entry.Value);

        File.Write(page.Reference, dictionary);
        _openPages.Remove(page);
        page.Content.Dispose();
    }

    /// <summary>
    /// Registers <paramref name="name"/> as a destination showing <paramref name="page"/> with
    /// (<paramref name="left"/>, <paramref name="top"/>) — in PDF user space — at the top-left of the window, at
    /// the zoom the reader already has. The first registration of a name wins; returns false for a repeat.
    /// </summary>
    public bool AddNamedDestination(string name, PdfReference page, double left, double top)
    {
        ArgumentNullException.ThrowIfNull(name);
        ThrowIfFinished();
        if (_destinations.ContainsKey(name))
            return false;

        _destinations.Add(name, new PdfArray(5) { page, PdfNames.XYZ, left, top, PdfValue.Null });
        return true;
    }

    /// <summary>A graphics state applying one opacity to fills and strokes alike.</summary>
    public PdfReference GetOpacityState(double alpha) => GetOpacityState(alpha, alpha);

    /// <summary>
    /// A graphics state setting constant opacity for fills (<c>/ca</c>) and strokes (<c>/CA</c>), each from 0
    /// (invisible) to 1 (opaque). One object is written per distinct pair, at the five-decimal precision the file
    /// records, and reused for every later request.
    /// </summary>
    public PdfReference GetOpacityState(double fillAlpha, double strokeAlpha)
    {
        CheckAlpha(fillAlpha, nameof(fillAlpha));
        CheckAlpha(strokeAlpha, nameof(strokeAlpha));
        ThrowIfFinished();

        (long, long) key = (Quantize(fillAlpha), Quantize(strokeAlpha));
        if (_opacityStates.TryGetValue(key, out PdfReference existing))
            return existing;

        PdfReference state = File.Write(new PdfDictionary
        {
            [PdfNames.Type] = PdfNames.ExtGState,
            [PdfNames.NonStrokingAlpha] = fillAlpha,
            [PdfNames.StrokingAlpha] = strokeAlpha,
        });

        _opacityStates.Add(key, state);
        return state;
    }

    /// <summary>
    /// Writes the page tree, the named destinations, the catalog and the information dictionary, then completes
    /// the file. Every page must have been ended, and every reserved object written.
    /// </summary>
    public void Finish()
    {
        ThrowIfFinished();
        if (_openPages.Count > 0)
            throw new InvalidOperationException($"{_openPages.Count} page(s) were begun but never ended.");

        CheckNotOwned(Catalog, "catalog", PdfNames.Type, PdfNames.Pages, PdfNames.Names);

        PdfDictionary catalog = new PdfDictionary(3 + Catalog.Count)
        {
            [PdfNames.Type] = PdfNames.Catalog,
            [PdfNames.Pages] = _pages.Write(),
        };

        if (_destinations.Count > 0)
        {
            PdfNameTree tree = new PdfNameTree();
            foreach (KeyValuePair<string, PdfArray> destination in _destinations)
                tree.Add(PdfString.FromText(destination.Key), destination.Value);

            catalog[PdfNames.Names] = new PdfDictionary { [PdfNames.Dests] = tree.Write(File) };
        }

        foreach (KeyValuePair<PdfName, PdfValue> entry in Catalog)
            catalog.Add(entry.Key, entry.Value);

        PdfReference root = File.Write(catalog);
        PdfReference? info = Info.IsEmpty ? null : File.Write(Info.ToDictionary());
        File.Finish(root, info);
        _finished = true;
    }

    /// <summary>Releases buffers, including those of pages never ended. Does not finish the document.</summary>
    public void Dispose()
    {
        foreach (PdfPage page in _openPages)
            page.Content.Dispose();

        File.Dispose();
    }

    private static void CheckAlpha(double alpha, string name)
    {
        if (!(alpha >= 0 && alpha <= 1))
            throw new ArgumentOutOfRangeException(name, alpha, "Opacity runs from 0 to 1.");
    }

    // The precision PdfNumbers writes values of this size with, so two alphas share an object exactly when they
    // would be written identically.
    private static long Quantize(double alpha) =>
        (long)Math.Round(alpha * 100_000, MidpointRounding.AwayFromZero);

    private static void CheckNotOwned(PdfDictionary entries, string owner, params PdfName[] owned)
    {
        foreach (PdfName key in owned)
        {
            if (entries.ContainsKey(key))
                throw new InvalidOperationException($"The writer sets the {owner}'s {key} entry itself.");
        }
    }

    private void ThrowIfFinished()
    {
        if (_finished)
            throw new InvalidOperationException("The document has been finished.");
    }
}
