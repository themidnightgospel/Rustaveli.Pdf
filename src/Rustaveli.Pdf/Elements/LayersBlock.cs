using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Draws children on top of one another in declaration order, sized by the primary layer.
/// </summary>
/// <remarks>
/// Layers before the primary one act as a background and those after it as an overlay, which is how watermarks
/// and underlays are expressed without a separate element for each.
/// </remarks>
public sealed class LayersBlock : Block
{
    public List<Layer> Layers { get; } = new List<Layer>();

    public override IEnumerable<Block?> GetChildren()
    {
        return Layers;
    }

    public override Fit Measure(Extent availableSpace, PlanContext context)
    {
        return Layers.FirstOrDefault((Layer layer) => layer.IsPrimary)?.Measure(availableSpace, context) ?? Fit.FullRender(Extent.Zero);
    }

    public override void Draw(Extent availableSpace, RenderContext context)
    {
        Fit plan = Measure(availableSpace, context.Layout);

        if (plan.IsWrap || plan.IsEmpty)
            return;

        // Every layer shares the whole box the stack occupies (ADR 0012). The primary layer decides how big that
        // box is when nothing else does, but a secondary layer aligned to the bottom must reach the bottom of the
        // box, not of the primary layer's content.
        foreach (Layer layer in Layers)
            layer.Draw(availableSpace, context);

        foreach (Layer layer in Layers.Where(layer => !layer.IsPrimary))
            layer.ResetState(includeDocumentProgress: false);
    }
}
