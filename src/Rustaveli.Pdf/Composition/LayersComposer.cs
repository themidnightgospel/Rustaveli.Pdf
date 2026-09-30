using Rustaveli.Pdf.Blocks;

namespace Rustaveli.Pdf;

/// <summary>
/// Adds layers drawn one over another across the same room, each in a frame of its own.
/// </summary>
/// <remarks>
/// Layers are painted in the order they are added, each over those before it. At most one of them is the base
/// layer: the layers together take the room it takes, and it is its content that flows from page to page. Layers
/// added before the base lie beneath it, as a background does; those after it lie over it. With no base layer the
/// layers take no room at all.
/// </remarks>
public sealed class LayersComposer
{
    private readonly LayersBlock _layers;

    internal LayersComposer(LayersBlock layers) => _layers = layers;

    /// <summary>
    /// Adds a layer drawn over the same room as the base layer, taking none of its own, and drawn again in full on
    /// every page.
    /// </summary>
    public IFrame Layer() => Add(isBase: false);

    /// <summary>Adds the base layer, whose size the layers take and whose content flows from page to page.</summary>
    /// <exception cref="CompositionException">A base layer has already been added.</exception>
    public IFrame BaseLayer()
    {
        if (_layers.Layers.Exists(layer => layer.IsBase))
            throw new CompositionException("Layers have one BaseLayer, whose size the stack takes; it is already declared.");

        return Add(isBase: true);
    }

    private IFrame Add(bool isBase)
    {
        Layer layer = new Layer { IsBase = isBase };
        _layers.Layers.Add(layer);
        return layer;
    }
}
