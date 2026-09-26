using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// One layer of a <see cref="LayersBlock"/>.
/// </summary>
internal sealed class Layer : EnclosingBlock
{
    /// <summary>
    /// Whether this layer determines the size of the stack. Exactly one layer should be primary; the others
    /// are painted into whatever space it claims.
    /// </summary>
    public bool IsBase { get; set; }
}
