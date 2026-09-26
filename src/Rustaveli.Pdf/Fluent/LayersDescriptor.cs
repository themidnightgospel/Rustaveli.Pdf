using Rustaveli.Pdf.Elements;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Builds a stack of overlapping layers.
/// </summary>
public sealed class LayersDescriptor(LayersElement element)
{
    /// <summary>Adds a layer that does not influence the size of the stack.</summary>
    public IFrame Layer() => Add(isPrimary: false);

    /// <summary>Adds the layer whose size the whole stack adopts. Declare exactly one.</summary>
    public IFrame PrimaryLayer() => Add(isPrimary: true);

    private IFrame Add(bool isPrimary)
    {
        Layer layer = new Layer { IsPrimary = isPrimary };
        element.Layers.Add(layer);
        return layer;
    }
}
