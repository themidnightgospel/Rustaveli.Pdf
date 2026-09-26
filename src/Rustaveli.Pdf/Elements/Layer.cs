using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// One layer of a <see cref="LayersElement"/>.
/// </summary>
public sealed class Layer : ContainerElement
{
    /// <summary>
    /// Whether this layer determines the size of the stack. Exactly one layer should be primary; the others
    /// are painted into whatever space it claims.
    /// </summary>
    public bool IsPrimary { get; set; }
}
