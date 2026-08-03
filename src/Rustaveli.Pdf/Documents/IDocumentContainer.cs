using Rustaveli.Pdf.Exceptions;

namespace Rustaveli.Pdf.Documents;

/// <summary>
/// Collects the page runs that make up a document.
/// </summary>
public interface IDocumentContainer
{
    /// <summary>Adds a run of pages sharing a size, margin and set of slots.</summary>
    void Page(Action<PageDescriptor> handler);
}
