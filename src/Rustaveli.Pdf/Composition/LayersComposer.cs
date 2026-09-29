using Rustaveli.Pdf.Blocks;

namespace Rustaveli.Pdf;

/// <summary>
/// Builds a stack of overlapping layers.
/// </summary>
public sealed class LayersComposer
{
    private readonly LayersBlock _block;

    internal LayersComposer(LayersBlock block) => _block = block;

    /// <summary>Adds a layer that does not influence the size of the stack.</summary>
    public IFrame Layer() => Add(isPrimary: false);

    /// <summary>Adds the layer whose size the whole stack adopts. Declare exactly one.</summary>
    public IFrame BaseLayer() => Add(isPrimary: true);

    private IFrame Add(bool isPrimary)
    {
        // Only the first base layer sizes the stack; a second would be drawn in a box measured by another's content.
        if (isPrimary && _block.Layers.Any(layer => layer.IsBase))
            throw new CompositionException("Layers have one BaseLayer, whose size the stack takes; it is already declared.");

        Layer layer = new Layer { IsBase = isPrimary };
        _block.Layers.Add(layer);
        return layer;
    }
}
