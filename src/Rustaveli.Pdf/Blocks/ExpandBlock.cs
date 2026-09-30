using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Takes all the room it is offered across, down, or both, however little its child needs: a fill or stroke around
/// it then reaches the edges of that room.
/// </summary>
internal sealed class ExpandBlock : EnclosingBlock
{
    public bool Horizontally { get; set; }

    public bool Vertically { get; set; }

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        Fit plan = base.PlanCore(availableSpace, context);

        Extent size = new Extent(
            Horizontally ? availableSpace.Width : plan.Size.Width,
            Vertically ? availableSpace.Height : plan.Size.Height);

        return Resized(plan, size);
    }
}
