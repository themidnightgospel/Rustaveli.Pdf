using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Draws layers one over another, in the order they are listed. The base layer decides how much room the whole takes
/// and whether it runs on; the others are drawn over the same room, and again in full on every page.
/// </summary>
internal sealed class LayersBlock : Block
{
    /// <summary>The layers, bottom first.</summary>
    public List<Layer> Layers { get; } = [];

    public override IEnumerable<Block?> GetChildren() => Layers;

    /// <summary>The base layer's plan; without one, the layers take no room.</summary>
    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        Base() is { } layer ? layer.Plan(availableSpace, context) : Fit.Complete(Extent.Zero);

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (!PlanCore(availableSpace, context.Planning).PlacesContent)
            return;

        // Each layer gets the whole room rather than the base layer's size, so a layer placed against the bottom
        // reaches the bottom of the box.
        foreach (Layer layer in Layers)
            layer.Render(availableSpace, context);

        foreach (Layer layer in Layers)
        {
            if (!layer.IsBase)
                layer.ResetState(includeDocumentProgress: false);
        }
    }

    private Layer? Base()
    {
        foreach (Layer layer in Layers)
        {
            if (layer.IsBase)
                return layer;
        }

        return null;
    }
}
