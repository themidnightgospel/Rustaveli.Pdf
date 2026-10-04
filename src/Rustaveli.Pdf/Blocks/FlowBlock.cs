using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Sets items side by side as words are set in a line, starting a new line wherever the next item would not fit
/// across, and carrying whole lines that do not fit down on to the next page.
/// </summary>
/// <remarks>
/// Items are never split: each is placed whole or left for the next line or page, and one with nothing to show takes
/// no place at all. One of no size takes no place either, but is drawn where it falls. Measuring and drawing share one
/// layout so the two can never disagree.
/// </remarks>
internal sealed class FlowBlock : Block
{
    private int _placed;

    public List<Block> Items { get; } = [];

    /// <summary>The gap between neighbouring items in a line.</summary>
    public float Gutter { get; set; }

    /// <summary>The gap between one line and the next.</summary>
    public float SpaceBetweenLines { get; set; }

    /// <summary>Where each line sits across the width, and how its gaps are spread.</summary>
    public FlowPlacement Placement { get; set; }

    /// <summary>Where items shorter than their line sit within it.</summary>
    public VerticalPlacement LineAlignment { get; set; } = VerticalPlacement.Top;

    internal override int ChildCount => Items.Count;

    internal override Block? ChildAt(int index) => Items[index];

    protected override void ResetOwnState() => _placed = 0;

    protected override object? SaveOwnProgress() => _placed;

    protected override void RestoreOwnProgress(object progress) => _placed = (int)progress;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        if (_placed >= Items.Count)
            return Fit.Nothing();

        Layout layout = Lay(availableSpace, context);

        if (layout.Lines.Count == 0)
        {
            return layout.Placed >= Items.Count
                ? Fit.Nothing()
                : Fit.Defer($"The next item of the flow does not fit in {availableSpace}.");
        }

        Extent size = new Extent(layout.Lines.Max(line => line.Width), layout.Height);

        return layout.Placed < Items.Count ? Fit.Partial(size) : Fit.Complete(size);
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (_placed >= Items.Count)
            return;

        Layout layout = Lay(availableSpace, context.Planning);
        bool rightToLeft = context.Planning.ReadingDirection == ReadingDirection.RightToLeft;

        for (int number = 0; number < layout.Lines.Count; number++)
        {
            Line line = layout.Lines[number];
            bool last = layout.Placed >= Items.Count && number == layout.Lines.Count - 1;
            (float start, float gap) = Spread(line, availableSpace.Width, last);
            float x = start;

            foreach ((int index, Extent size, bool takesPlace) in line.Items)
            {
                float y = line.Top + LineAlignment switch
                {
                    VerticalPlacement.Middle => (line.Height - size.Height) / 2,
                    VerticalPlacement.Bottom => line.Height - size.Height,
                    _ => 0f,
                };

                Offset at = new Offset(rightToLeft ? availableSpace.Width - x - size.Width : x, y);
                context.Surface.MoveOrigin(at);
                context.RenderAllotted(Items[index], size, availableSpace.Height);
                context.Surface.MoveOrigin(at.Reverse());

                // An item of no size has no gap after it.
                x += takesPlace ? size.Width + gap : 0f;
            }
        }

        _placed = layout.Placed;
    }

    /// <summary>
    /// Where a line's first item starts and the gap after each, for a line in a box <paramref name="width"/> wide. The
    /// flow's last line is not justified, as a paragraph's is not.
    /// </summary>
    private (float Start, float Gap) Spread(Line line, float width, bool last)
    {
        float extra = Math.Max(0, width - line.Width);
        int count = line.Shown;

        // A line may hold only items of no size, which have no space around them to share.
        return Placement switch
        {
            FlowPlacement.Center => (extra / 2, Gutter),
            FlowPlacement.Right => (extra, Gutter),
            FlowPlacement.Justify when count > 1 && !last => (0, Gutter + (extra / (count - 1))),
            FlowPlacement.SpaceAround => (extra / Math.Max(1, count) / 2, Gutter + (extra / Math.Max(1, count))),
            _ => (0, Gutter),
        };
    }

    /// <summary>
    /// Lines of the items still to place, as many as fit down <paramref name="availableSpace"/>, each item measured
    /// in the whole space and placed only if it is complete.
    /// </summary>
    private Layout Lay(Extent availableSpace, PlanContext context)
    {
        List<Line> lines = [];
        List<(int Index, Extent Size, bool TakesPlace)> items = [];
        int shown = 0;
        int first = _placed;
        float width = 0f;
        float height = 0f;
        float top = 0f;
        int index = _placed;

        for (; index < Items.Count; index++)
        {
            Fit plan = Items[index].Plan(availableSpace, context);

            if (!plan.IsNothing && !plan.IsComplete)
                break;

            // Used up or hidden, so it takes no place; a line that fails to fit still starts after it.
            if (plan.IsNothing)
            {
                if (items.Count == 0)
                    first = index + 1;

                continue;
            }

            // Of no size, it takes no place and no gap, but goes with the line it falls in and is drawn there: what it
            // does as it is drawn — an anchor, a marker hidden the first time — must still happen, as in a stack.
            if (plan.Size.Width <= Extent.Epsilon && plan.Size.Height <= Extent.Epsilon)
            {
                items.Add((index, Extent.Zero, false));
                continue;
            }

            Extent size = plan.Size;

            if (shown > 0 && width + Gutter + size.Width > availableSpace.Width + Extent.Epsilon)
            {
                if (!Close())
                    return new Layout(lines, first, top);

                first = index;
            }

            width = shown == 0 ? size.Width : width + Gutter + size.Width;
            height = Math.Max(height, size.Height);
            items.Add((index, size, true));
            shown++;
        }

        if (items.Count > 0 && !Close())
            return new Layout(lines, first, top);

        return new Layout(lines, index, top);

        // Ends the line being built, if it fits below the lines before it.
        bool Close()
        {
            float lineTop = lines.Count == 0 ? 0f : top + SpaceBetweenLines;

            if (lineTop + height > availableSpace.Height + Extent.Epsilon)
                return false;

            lines.Add(new Line([.. items], shown, width, height, lineTop));
            top = lineTop + height;
            items.Clear();
            shown = 0;
            width = 0f;
            height = 0f;
            return true;
        }
    }

    /// <summary>
    /// A line: its items, their sizes and whether they take a place in it, how many do, how wide and tall it is, and
    /// where it starts.
    /// </summary>
    private readonly record struct Line(IReadOnlyList<(int Index, Extent Size, bool TakesPlace)> Items, int Shown, float Width, float Height, float Top);

    /// <summary>The lines that fit, the index of the first item left over, and the height they take.</summary>
    private readonly record struct Layout(List<Line> Lines, int Placed, float Height);
}
