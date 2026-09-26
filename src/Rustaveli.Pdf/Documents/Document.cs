using Rustaveli.Pdf.Exceptions;

namespace Rustaveli.Pdf.Documents;

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

    public DocumentInfo Metadata { get; } = new DocumentInfo();

    internal IReadOnlyList<Section> Pages => _pages;

    private Document()
    {
    }

    /// <summary>
    /// Builds a document by invoking <paramref name="compose" />, which declares one or more page runs.
    /// </summary>
    public static Document Create(Action<IComposition> compose)
    {
        ArgumentNullException.ThrowIfNull(compose, "compose");
        Document document = new Document();
        try
        {
            compose(document);
        }
        catch (Exception ex) when (!(ex is CompositionException))
        {
            throw new CompositionException("The document could not be composed. See the inner exception for details.", ex);
        }
        return document;
    }

    void IComposition.Page(Action<Section> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, "handler");
        Section pageDescriptor = new Section();
        handler(pageDescriptor);
        _pages.Add(pageDescriptor);
    }
}
