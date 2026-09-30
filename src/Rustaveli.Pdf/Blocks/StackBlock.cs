using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Sets its items one below another, and runs on to the next page between items, or within one that splits.
/// </summary>
/// <remarks>
/// Planning and drawing share one walk down the items, so that what is drawn on a page is exactly what was planned
/// for it: drawing only records, in addition, where each item went.
/// </remarks>
internal sealed class StackBlock : Block
{
    /// <summary>How many items, from the top, have been drawn in full on earlier pages.</summary>
    private int _finished;

    /// <summary>The items, top to bottom.</summary>
    public List<Block> Items { get; } = [];

    /// <summary>The room left between two items that both take some height.</summary>
    public float SpaceBetween { get; set; }

    public override IEnumerable<Block?> GetChildren() => Items;

    protected override void ResetOwnState() => _finished = 0;

    protected override object? SaveOwnProgress() => _finished;

    protected override void RestoreOwnProgress(object progress) => _finished = (int)progress;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context) =>
        Walk(availableSpace, context, placed: null).Outcome;

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        List<Placed> placed = [];
        Page page = Walk(availableSpace, context.Planning, placed);

        if (placed.Count == 0)
            return;

        foreach (Placed item in placed)
        {
            Offset top = new Offset(0, item.Top);
            context.Surface.MoveOrigin(top);
            context.RenderAllotted(Items[item.Index], new Extent(availableSpace.Width, item.Height), item.Offered);
            context.Surface.MoveOrigin(top.Reverse());
        }

        _finished = page.Resume;
    }

    /// <summary>
    /// Goes down the items not yet finished, placing each that fits in the room left, until one does not, one splits,
    /// or none are left. Drawing passes a list to record each item placed in; planning passes none.
    /// </summary>
    private Page Walk(Extent room, PlanContext context, List<Placed>? placed)
    {
        float used = 0f;
        float widest = 0f;
        bool anyPlaced = false;
        bool anyTall = false;
        int index = _finished;

        // The item the page ends before or within, if it ends early; an item past it is still to come.
        int? stoppedAt = null;

        for (; index < Items.Count; index++)
        {
            float left = room.Height - used;

            // Room already overdrawn — by an item planned as taller than the room — takes nothing more.
            if (left < -Extent.Epsilon)
            {
                stoppedAt = index;
                break;
            }

            Block item = Items[index];
            float gap = anyTall ? SpaceBetween : 0f;

            // With no room for the gap, only an item taking no height may still be set here: an anchor or a marker
            // has to be drawn on this page for what it records.
            bool gapFits = left - gap >= -Extent.Epsilon;
            float offered = gapFits ? left - gap : left;
            Fit plan = item.Plan(new Extent(room.Width, offered), context);

            if (plan.IsNothing)
                continue;

            bool tall = plan.Size.Height > Extent.Epsilon;

            if (plan.IsDeferred || (!gapFits && tall))
            {
                if (!anyPlaced && plan.IsDeferred)
                    return new Page(Fit.Defer(plan.DeferReason ?? "An item did not fit in the available space."), _finished);

                stoppedAt = index;
                break;
            }

            // A gap belongs between two items that take height; one taking none is set flush where the gap would be.
            float top = tall ? used + gap : used;
            placed?.Add(new Placed(index, top, plan.Size.Height, offered));

            anyPlaced = true;
            anyTall |= tall;
            widest = Math.Max(widest, plan.Size.Width);
            used = top + plan.Size.Height;

            if (plan.IsPartial)
            {
                // The item runs on to the next page, and stays the current one until it is finished.
                stoppedAt = index;
                break;
            }
        }

        if (!anyPlaced)
        {
            return stoppedAt is null
                ? new Page(Fit.Nothing(), _finished)
                : new Page(Fit.Defer("No item fitted in the available space."), _finished);
        }

        Extent size = new Extent(widest, used);
        return stoppedAt is { } resume
            ? new Page(Fit.Partial(size), resume)
            : new Page(Fit.Complete(size), Items.Count);
    }

    /// <summary>What a walk found: the stack's outcome, and the first item to start from on the next page.</summary>
    private readonly record struct Page(Fit Outcome, int Resume);

    /// <summary>
    /// An item placed on the page: where it starts down the page, the height it takes, and the height it was
    /// planned in.
    /// </summary>
    private readonly record struct Placed(int Index, float Top, float Height, float Offered);
}
