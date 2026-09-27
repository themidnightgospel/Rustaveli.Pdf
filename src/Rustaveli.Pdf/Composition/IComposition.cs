namespace Rustaveli.Pdf;

/// <summary>
/// Collects the page runs that make up a document.
/// </summary>
public interface IComposition
{
    /// <summary>The document's named styles, defined here before the content that names them.</summary>
    StyleSheet Styles { get; }

    /// <summary>Adds a run of pages sharing a size, margin and set of slots.</summary>
    void Section(Action<Section> handler);
}
