using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Reports the full available size on the chosen axes regardless of how little the child needs.
/// </summary>
public sealed class ExtendElement : ContainerElement
{
    public bool ExtendHorizontal { get; set; }

    public bool ExtendVertical { get; set; }

    public override SpacePlan Measure(Size availableSpace, LayoutContext context)
    {
        SpacePlan childPlan = Child?.Measure(availableSpace, context) ?? SpacePlan.FullRender(Size.Zero);

        if (childPlan.IsWrap)
            return childPlan;

        if (childPlan.IsEmpty)
            return SpacePlan.Empty();

        Size size = new Size(
            ExtendHorizontal ? availableSpace.Width : childPlan.Size.Width,
            ExtendVertical ? availableSpace.Height : childPlan.Size.Height);

        return childPlan.IsFullRender ? SpacePlan.FullRender(size) : SpacePlan.PartialRender(size);
    }
}
