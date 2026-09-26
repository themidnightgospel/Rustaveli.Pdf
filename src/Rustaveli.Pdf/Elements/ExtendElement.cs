using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Reports the full available size on the chosen axes regardless of how little the child needs.
/// </summary>
public sealed class ExtendElement : EnclosingBlock
{
    public bool ExtendHorizontal { get; set; }

    public bool ExtendVertical { get; set; }

    public override Fit Measure(Extent availableSpace, PlanContext context)
    {
        Fit childPlan = Child?.Measure(availableSpace, context) ?? Fit.FullRender(Extent.Zero);

        if (childPlan.IsWrap)
            return childPlan;

        if (childPlan.IsEmpty)
            return Fit.Empty();

        Extent size = new Extent(
            ExtendHorizontal ? availableSpace.Width : childPlan.Size.Width,
            ExtendVertical ? availableSpace.Height : childPlan.Size.Height);

        return childPlan.IsFullRender ? Fit.FullRender(size) : Fit.PartialRender(size);
    }
}
