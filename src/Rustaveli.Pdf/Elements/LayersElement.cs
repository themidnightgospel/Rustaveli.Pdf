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
public sealed class LayersElement : Element
{
    public List<Layer> Layers { get; } = new List<Layer>();

    public override IEnumerable<Element?> GetChildren()
    {
        return Layers;
    }

    public override SpacePlan Measure(Size availableSpace, LayoutContext context)
    {
        return Layers.FirstOrDefault((Layer layer) => layer.IsPrimary)?.Measure(availableSpace, context) ?? SpacePlan.FullRender(Size.Zero);
    }

    public override void Draw(Size availableSpace, DrawContext context)
    {
        SpacePlan spacePlan = Measure(availableSpace, context.Layout);
        if (spacePlan.IsWrap || spacePlan.IsEmpty)
        {
            return;
        }
        foreach (Layer layer in Layers)
        {
            layer.Draw(spacePlan.Size, context);
        }
        foreach (Layer item in Layers.Where((Layer layer) => !layer.IsPrimary))
        {
            item.ResetState(includeDocumentProgress: false);
        }
    }
}
