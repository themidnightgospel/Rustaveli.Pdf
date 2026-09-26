using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Reports the full available size on the chosen axes regardless of how little the child needs.
/// </summary>
internal sealed class ExpandBlock : EnclosingBlock
{
    public bool ExtendHorizontal { get; set; }

    public bool ExtendVertical { get; set; }

    public override Fit Plan(Extent availableSpace, PlanContext context)
    {
        Fit childPlan = Child?.Plan(availableSpace, context) ?? Fit.Complete(Extent.Zero);

        if (childPlan.IsDeferred)
            return childPlan;

        if (childPlan.IsNothing)
            return Fit.Nothing();

        Extent size = new Extent(
            ExtendHorizontal ? availableSpace.Width : childPlan.Size.Width,
            ExtendVertical ? availableSpace.Height : childPlan.Size.Height);

        return childPlan.IsComplete ? Fit.Complete(size) : Fit.Partial(size);
    }
}
