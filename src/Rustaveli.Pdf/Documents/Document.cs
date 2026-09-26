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
public sealed class Document : IDocumentContainer
{
    private readonly List<PageDescriptor> _pages = new List<PageDescriptor>();

    public DocumentMetadata Metadata { get; } = new DocumentMetadata();

    internal IReadOnlyList<PageDescriptor> Pages => _pages;

    private Document()
    {
    }

    /// <summary>
    /// Builds a document by invoking <paramref name="compose" />, which declares one or more page runs.
    /// </summary>
    public static Document Create(Action<IDocumentContainer> compose)
    {
        ArgumentNullException.ThrowIfNull(compose, "compose");
        Document document = new Document();
        try
        {
            compose(document);
        }
        catch (Exception ex) when (!(ex is DocumentComposeException))
        {
            throw new DocumentComposeException("The document could not be composed. See the inner exception for details.", ex);
        }
        return document;
    }

    void IDocumentContainer.Page(Action<PageDescriptor> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, "handler");
        PageDescriptor pageDescriptor = new PageDescriptor();
        handler(pageDescriptor);
        _pages.Add(pageDescriptor);
    }
}
