using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Stacks children vertically, flowing whatever does not fit onto the next page.
/// </summary>
/// <remarks>
/// The column remembers how many children it has finished so that a continuation on the following page resumes
/// rather than restarting. A child that only partially rendered keeps its own internal position, so the column
/// deliberately does not advance past it.
/// </remarks>
public sealed class ColumnElement : Element
{
    private int _completedItems;

    public List<Element> Items { get; } = [];

    /// <summary>Vertical gap inserted between consecutive items.</summary>
    public float Spacing { get; set; }

    public override IEnumerable<Element?> GetChildren() => Items;

    protected override void ResetOwnState() => _completedItems = 0;

    public override SpacePlan Measure(Size availableSpace, LayoutContext context)
    {
        LayoutResult result = Layout(availableSpace, context, static (_, _, _) => { });

        return result.ToSpacePlan();
    }

    public override void Draw(Size availableSpace, DrawContext context)
    {
        ICanvas canvas = context.Canvas;
        float offset = 0f;

        LayoutResult result = Layout(availableSpace, context.Layout, (item, itemSpace, top) =>
        {
            Position delta = new Position(0, top - offset);
            canvas.Translate(delta);
            offset = top;
            item.Draw(itemSpace, context);
        });

        canvas.Translate(new Position(0, -offset));

        // Exhausted and wrapped results carry no progress, so leave the cursor where it was.
        if (result.DrewContent)
            _completedItems = result.CompletedItems;
    }

    /// <summary>
    /// Walks the remaining items, accumulating height and invoking <paramref name="onItem"/> for each one that
    /// fits. Measuring and drawing share this so the two passes can never disagree about what fits.
    /// </summary>
    private LayoutResult Layout(Size availableSpace, LayoutContext context, Action<Element, Size, float> onItem)
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
            float spacing = hasVisibleContent ? Spacing : 0f;
            float remainingHeight = availableSpace.Height - totalHeight - spacing;

            if (remainingHeight < -Size.Epsilon)
            {
                pending = true;
                break;
            }

            Size itemSpace = new Size(availableSpace.Width, remainingHeight);
            SpacePlan plan = Items[index].Measure(itemSpace, context);

            if (plan.IsEmpty)
            {
                completed = index + 1;
                continue;
            }

            if (plan.IsWrap)
            {
                // Nothing rendered yet means even a fresh page would look identical; report the wrap upwards so
                // the engine can distinguish "needs a new page" from "can never fit".
                if (!drewAnything)
                    return LayoutResult.Wrapped(plan.WrapReason ?? "An item did not fit in the available space.");

                pending = true;
                break;
            }

            // Only an item that actually occupies space earns a gap before it. Hidden content still has to be
            // drawn — a "skip once" marker advances its state during Draw — but it must leave no visible trace,
            // otherwise toggling a section on and off would shift everything below it.
            bool occupiesSpace = plan.Size.Height > Size.Epsilon;

            if (occupiesSpace)
                totalHeight += spacing;

            onItem(Items[index], itemSpace, totalHeight);

            totalHeight += plan.Size.Height;
            maxWidth = Math.Max(maxWidth, plan.Size.Width);
            drewAnything = true;
            hasVisibleContent |= occupiesSpace;

            if (plan.IsPartialRender)
            {
                pending = true;
                break;
            }

            completed = index + 1;
        }

        if (!drewAnything)
            return completed >= Items.Count ? LayoutResult.Exhausted() : LayoutResult.Wrapped("No item fitted in the available space.");

        return new LayoutResult(new Size(maxWidth, totalHeight), completed, pending || completed < Items.Count);
    }

    private readonly record struct LayoutResult(Size Size, int CompletedItems, bool HasMore, string? WrapReason = null, bool IsExhausted = false)
    {
        public static LayoutResult Exhausted() => new(Size.Zero, 0, false, null, true);

        public static LayoutResult Wrapped(string reason) => new(Size.Zero, 0, false, reason);

        public bool DrewContent => !IsExhausted && WrapReason is null;

        public SpacePlan ToSpacePlan()
        {
            if (IsExhausted)
                return SpacePlan.Empty();

            if (WrapReason is not null)
                return SpacePlan.Wrap(WrapReason);

            return HasMore ? SpacePlan.PartialRender(Size) : SpacePlan.FullRender(Size);
        }
    }
}
