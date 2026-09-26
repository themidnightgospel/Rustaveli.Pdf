namespace Rustaveli.Pdf.Documents;

/// <summary>
/// Collects the page runs that make up a document.
/// </summary>
public interface IComposition
{
    /// <summary>Adds a run of pages sharing a size, margin and set of slots.</summary>
    void Page(Action<Section> handler);
}
