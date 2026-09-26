namespace Rustaveli.Pdf.Layout;

/// <summary>
/// Per-generation state describing where in the document the engine currently is.
/// </summary>
/// <remarks>
/// Content such as "Page 3 of 12" cannot be resolved during a single pass, because the total is only known once
/// pagination has finished. The generator therefore runs the document more than once: counting passes with
/// <see cref="IsPageCountKnown"/> false, purely to count pages, then a pass that draws for real.
/// Blocks that depend on the total must read it through this type rather than caching it.
/// </remarks>
internal sealed class Pagination
{
    private readonly Dictionary<string, AnchorPages> _known = [];
    private Dictionary<string, AnchorPages> _recording = [];

    /// <summary>The one-based number of the page being laid out.</summary>
    public int Folio { get; internal set; } = 1;

    /// <summary>The total page count. Meaningful only when <see cref="IsPageCountKnown"/> is true.</summary>
    public int PageCount { get; internal set; }

    /// <summary>False during the counting pass, true while drawing the final output.</summary>
    public bool IsPageCountKnown { get; internal set; }

    /// <summary>
    /// Records that an anchor's content was drawn on <paramref name="folio"/>. Content that flows across pages is
    /// drawn once per page, so an anchor spans from the first page recorded in a pass to the last.
    /// </summary>
    internal void RegisterAnchor(string name, int folio) =>
        _recording[name] = _recording.TryGetValue(name, out AnchorPages pages)
            ? new AnchorPages(Math.Min(pages.First, folio), Math.Max(pages.Last, folio))
            : new AnchorPages(folio, folio);

    /// <summary>The page an anchor begins on, or null if it has not been seen yet.</summary>
    public int? FolioOf(string name) => Find(name)?.First;

    /// <summary>The page an anchor's content ends on, or null if it has not been seen yet.</summary>
    public int? LastFolioOf(string name) => Find(name)?.Last;

    /// <summary>
    /// Prepares for another rendering pass over the same document.
    /// </summary>
    /// <remarks>
    /// Anchors are carried over: a reference can only name a page that has not been drawn yet — a table of contents,
    /// "continued on page 7" — because an earlier pass already found it. Each pass records afresh, and references
    /// read what the last complete pass found, so a running foot on the second page of a chapter already knows the
    /// page the chapter ends on.
    /// </remarks>
    internal void ResetForNewPass()
    {
        foreach (KeyValuePair<string, AnchorPages> anchor in _recording)
            _known[anchor.Key] = anchor.Value;

        _recording = [];
        Folio = 1;
    }

    private AnchorPages? Find(string name) =>
        _known.TryGetValue(name, out AnchorPages settled) ? settled
        : _recording.TryGetValue(name, out AnchorPages partial) ? partial
        : null;

    /// <summary>The first and last page an anchor's content was drawn on.</summary>
    private readonly record struct AnchorPages(int First, int Last);
}
