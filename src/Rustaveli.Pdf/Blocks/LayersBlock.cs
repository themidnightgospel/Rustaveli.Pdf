using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Draws children on top of one another in declaration order, sized by the primary layer.
/// </summary>
/// <remarks>
/// Layers before the primary one act as a background and those after it as an overlay, which is how watermarks
/// and underlays are expressed without a separate element for each.
/// </remarks>
internal sealed class LayersBlock : Block
{
    public List<Layer> Layers { get; } = new List<Layer>();

    public override IEnumerable<Block?> GetChildren()
    {
        return Layers;
    }

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        return Layers.FirstOrDefault(layer => layer.IsBase)?.Plan(availableSpace, context) ?? Fit.Complete(Extent.Zero);
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        Fit plan = Plan(availableSpace, context.Planning);

        if (plan.IsDeferred || plan.IsNothing)
            return;

        // Every layer shares the whole box the stack occupies (ADR 0012). The primary layer decides how big that
        // box is when nothing else does, but a secondary layer aligned to the bottom must reach the bottom of the
        // box, not of the primary layer's content.
        foreach (Layer layer in Layers)
            layer.Render(availableSpace, context);

        foreach (Layer layer in Layers.Where(layer => !layer.IsBase))
            layer.ResetState(includeDocumentProgress: false);
    }
}
