using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Turns its child clockwise by whole quarter turns, and lays it out turned: after an odd number of turns the child's
/// width runs down the page and its height across it.
/// </summary>
/// <remarks>
/// Turning by any other angle leaves layout alone, and is <see cref="RotateBlock"/>'s job.
/// </remarks>
internal sealed class TurnBlock : EnclosingBlock
{
    private int _quarterTurns;

    /// <summary>
    /// How many quarter turns clockwise, from 0 to 3. Any number may be set, and is taken round a whole turn: -1 is 3,
    /// and 5 is 1.
    /// </summary>
    public int QuarterTurns
    {
        get => _quarterTurns;
        set => _quarterTurns = ((value % 4) + 4) % 4;
    }

    /// <summary>Whether the turn lays the child's width down the page.</summary>
    private bool Sideways => _quarterTurns % 2 == 1;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        Fit plan = base.PlanCore(Turned(availableSpace), context);
        return Resized(plan, Turned(plan.Size));
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (Child is null)
            return;

        Extent room = Turned(availableSpace);

        if (!Child.Plan(room, context.Planning).PlacesContent)
            return;

        if (_quarterTurns == 0)
        {
            Child.Render(room, context);
            return;
        }

        // The corner of the box the child's own top left comes to once turned, so that the turned child lies over the
        // box rather than beside it.
        Offset corner = _quarterTurns switch
        {
            1 => new Offset(availableSpace.Width, 0),
            2 => new Offset(availableSpace.Width, availableSpace.Height),
            _ => new Offset(0, availableSpace.Height),
        };

        context.Surface.Save();
        context.Surface.MoveOrigin(corner);
        context.Surface.RotateClockwise(_quarterTurns * 90f);
        Child.Render(room, context);
        context.Surface.Restore();
    }

    private Extent Turned(Extent size) => Sideways ? new Extent(size.Height, size.Width) : size;
}
