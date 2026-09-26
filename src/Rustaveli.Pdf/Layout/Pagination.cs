namespace Rustaveli.Pdf.Layout;

/// <summary>
/// Per-generation state describing where in the document the engine currently is.
/// </summary>
/// <remarks>
/// Content such as "Page 3 of 12" cannot be resolved during a single pass, because the total is only known once
/// pagination has finished. The generator therefore runs the document twice: a first pass with
/// <see cref="IsPageCountKnown"/> false purely to count pages, then a second pass that draws for real.
/// Elements that depend on the total must read it through this type rather than caching it.
/// </remarks>
internal sealed class Pagination
{
    /// <summary>The one-based number of the page being laid out.</summary>
    public int Folio { get; internal set; } = 1;

    /// <summary>The total page count. Meaningful only when <see cref="IsPageCountKnown"/> is true.</summary>
    public int PageCount { get; internal set; }

    /// <summary>False during the counting pass, true while drawing the final output.</summary>
    public bool IsPageCountKnown { get; internal set; }

    private readonly Dictionary<string, int> _destinations = [];

    /// <summary>Records the page a named destination resolved to, so later passes can link to it.</summary>
    internal void RegisterAnchor(string name, int pageNumber) => _destinations[name] = pageNumber;

    /// <summary>The page a named destination lives on, or null if it has not been seen yet.</summary>
    public int? FolioOf(string name) =>
        _destinations.TryGetValue(name, out int page) ? page : null;

    /// <summary>
    /// Prepares for another rendering pass over the same document.
    /// </summary>
    /// <remarks>
    /// Destinations are deliberately kept. They are discovered during the counting pass, and carrying them into
    /// the drawing pass is the only way a reference can name a page that has not been drawn yet — which is the
    /// entire point of running two passes. The drawing pass simply overwrites each entry with the same value as
    /// it reaches it.
    /// </remarks>
    internal void ResetForNewPass() => Folio = 1;
}
