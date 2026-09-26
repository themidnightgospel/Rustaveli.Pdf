using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Stacks children vertically, flowing whatever does not fit onto the next page.
/// </summary>
/// <remarks>
/// The column remembers how many children it has finished so that a continuation on the following page resumes
/// rather than restarting. A child that only partially rendered keeps its own internal position, so the column
/// deliberately does not advance past it.
/// </remarks>
internal sealed class StackBlock : Block
{
    private int _completedItems;

    public List<Block> Items { get; } = [];

    /// <summary>Vertical gap inserted between consecutive items.</summary>
    public float SpaceBetween { get; set; }

    public override IEnumerable<Block?> GetChildren() => Items;

    protected override void ResetOwnState() => _completedItems = 0;

    public override Fit Plan(Extent availableSpace, PlanContext context)
    {
        LayoutResult result = Layout(availableSpace, context, static (_, _, _) => { });

        return result.ToSpacePlan();
    }

    public override void Render(Extent availableSpace, RenderContext context)
    {
        ISurface canvas = context.Surface;
        float offset = 0f;

        LayoutResult result = Layout(availableSpace, context.Planning, (item, itemSpace, top) =>
        {
            Offset delta = new Offset(0, top - offset);
            canvas.Translate(delta);
            offset = top;
            item.Render(itemSpace, context);
        });

        canvas.Translate(new Offset(0, -offset));

        // Exhausted and wrapped results carry no progress, so leave the cursor where it was.
        if (result.DrewContent)
            _completedItems = result.CompletedItems;
    }

    /// <summary>
    /// Walks the remaining items, accumulating height and invoking <paramref name="onItem"/> for each one that
    /// fits. Measuring and drawing share this so the two passes can never disagree about what fits.
    /// </summary>
    private LayoutResult Layout(Extent availableSpace, PlanContext context, Action<Block, Extent, float> onItem)
    {
        if (_completedItems >= Items.Count)
            return LayoutResult.Exhausted();

        float totalHeight = 0f;
        float maxWidth = 0f;
        int completed = _completedItems;
        bool drewAnything = false;
        bool hasVisibleContent = false;
        bool pending = false;

        for (int index = _completedItems; index < Items.Count; index++)
        {
            float spacing = hasVisibleContent ? SpaceBetween : 0f;
            float heightLeft = availableSpace.Height - totalHeight;

            // Offered less than nothing: there is no box to hand any item, not even one of no height.
            if (heightLeft < -Extent.Epsilon)
            {
                pending = true;
                break;
            }

            // Room remains, but not for the gap — as when a column is drawn at exactly the height it measured. An
            // item that occupies no height needs no gap, though, and still has to be drawn on this page: its side
            // effects (a destination, a "skip once" state change) belong here. Offer it the space without the gap
            // and keep it only if it claims none.
            bool gapOverflows = heightLeft - spacing < -Extent.Epsilon;

            Extent itemSpace = new Extent(availableSpace.Width, gapOverflows ? Math.Max(0f, heightLeft) : heightLeft - spacing);
            Fit plan = Items[index].Plan(itemSpace, context);

            if (gapOverflows && !plan.IsNothing && (plan.IsDeferred || plan.Size.Height > Extent.Epsilon))
            {
                pending = true;
                break;
            }

            if (plan.IsNothing)
            {
                completed = index + 1;
                continue;
            }

            if (plan.IsDeferred)
            {
                // Nothing rendered yet means even a fresh page would look identical; report the wrap upwards so
                // the engine can distinguish "needs a new page" from "can never fit".
                if (!drewAnything)
                    return LayoutResult.Wrapped(plan.DeferReason ?? "An item did not fit in the available space.");

                pending = true;
                break;
            }

            // Only an item that actually occupies space earns a gap before it. Hidden content still has to be
            // drawn — a "skip once" marker advances its state during Draw — but it must leave no visible trace,
            // otherwise toggling a section on and off would shift everything below it.
            bool occupiesSpace = plan.Size.Height > Extent.Epsilon;

            if (occupiesSpace)
                totalHeight += spacing;

            // The item's final size: the column's full width, and the height it measured (ADR 0012).
            onItem(Items[index], new Extent(availableSpace.Width, plan.Size.Height), totalHeight);

            totalHeight += plan.Size.Height;
            maxWidth = Math.Max(maxWidth, plan.Size.Width);
            drewAnything = true;
            hasVisibleContent |= occupiesSpace;

            if (plan.IsPartial)
            {
                pending = true;
                break;
            }

            completed = index + 1;
        }

        if (!drewAnything)
            return completed >= Items.Count ? LayoutResult.Exhausted() : LayoutResult.Wrapped("No item fitted in the available space.");

        return new LayoutResult(new Extent(maxWidth, totalHeight), completed, pending || completed < Items.Count);
    }

    private readonly record struct LayoutResult(Extent Size, int CompletedItems, bool HasMore, string? DeferReason = null, bool IsExhausted = false)
    {
        public static LayoutResult Exhausted() => new(Extent.Zero, 0, false, null, true);

        public static LayoutResult Wrapped(string reason) => new(Extent.Zero, 0, false, reason);

        public bool DrewContent => !IsExhausted && DeferReason is null;

        public Fit ToSpacePlan()
        {
            if (IsExhausted)
                return Fit.Nothing();

            if (DeferReason is not null)
                return Fit.Defer(DeferReason);

            return HasMore ? Fit.Partial(Size) : Fit.Complete(Size);
        }
    }
}
