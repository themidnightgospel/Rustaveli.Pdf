using System.Globalization;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Sets its items side by side in one row, and runs on to the next page with every item that has not finished kept
/// in the same place across the row.
/// </summary>
/// <remarks>
/// Each item's width is decided by the width of the row alone, never by the height left on the page, so an item keeps
/// its place from page to page and the items beside it do not move.
/// </remarks>
internal sealed class ColumnsBlock : Block
{
    /// <summary>For each item, whether it has been drawn in full.</summary>
    private bool[] _finished = [];

    /// <summary>
    /// The widths last worked out, and the width of the row they were worked out for: a natural item is measured to
    /// find its width, which is too costly to repeat every time the row is planned or drawn.
    /// </summary>
    private float[]? _widths;

    private float _widthsFor;

    /// <summary>The items, in reading order.</summary>
    public List<ColumnSlot> Items { get; } = [];

    /// <summary>The room left between two neighbouring items.</summary>
    public float Gutter { get; set; }

    /// <summary>
    /// When above zero, the row is laid on a grid of this many equal columns, and each item's
    /// <see cref="ColumnSlot.Value"/> is how many of them it spans.
    /// </summary>
    public int GridColumns { get; set; }

    /// <summary>
    /// On a grid, how many of its columns, with the gutter after each, are left unused before the first item: a half
    /// or a whole number, to set a row the items do not fill in the middle or at the end.
    /// </summary>
    public float LeadingGridColumns { get; set; }

    /// <summary>Which way the row reads, when it reads other than the content around it.</summary>
    public ReadingDirection? ReadingDirection { get; set; }

    private bool OnGrid => GridColumns > 0;

    internal override int ChildCount => Items.Count;

    internal override Block? ChildAt(int index) => Items[index];

    protected override void ResetOwnState()
    {
        _finished = [];
        _widths = null;
    }

    /// <remarks>
    /// The finished flags are changed in place as items finish, so the copy saved must be one of its own, and so must
    /// the copy restored: the same progress may be returned to more than once.
    /// </remarks>
    protected override object? SaveOwnProgress() => new Saved((bool[])_finished.Clone(), _widths, _widthsFor);

    protected override void RestoreOwnProgress(object progress)
    {
        Saved saved = (Saved)progress;
        _finished = (bool[])saved.Finished.Clone();
        _widths = saved.Widths;
        _widthsFor = saved.WidthsFor;
    }

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        if (Items.Count == 0)
            return Fit.Complete(Extent.Zero);

        float needed = Gutter * (Items.Count - 1) + (OnGrid ? 0f : FixedWidth());

        if (needed > availableSpace.Width + Extent.Epsilon)
        {
            return Fit.Defer(string.Format(
                CultureInfo.InvariantCulture,
                "The row's fixed columns and gutters need {0:0.#} pt across, but only {1:0.#} pt is available.",
                needed,
                availableSpace.Width));
        }

        float[] widths = Widths(availableSpace.Width, context);
        bool[] finished = Finished();
        float tallest = 0f;
        bool anyContent = false;
        bool anyPartial = false;

        for (int index = 0; index < Items.Count; index++)
        {
            if (finished[index])
                continue;

            Fit plan = Items[index].Plan(new Extent(widths[index], availableSpace.Height), context);

            if (plan.IsDeferred)
                return plan;

            if (plan.IsNothing)
                continue;

            anyContent = true;
            anyPartial |= plan.IsPartial;
            tallest = Math.Max(tallest, plan.Size.Height);
        }

        if (!anyContent)
            return Fit.Nothing();

        Extent size = new Extent(availableSpace.Width, tallest);
        return anyPartial ? Fit.Partial(size) : Fit.Complete(size);
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        Fit row = PlanCore(availableSpace, context.Planning);

        if (!row.PlacesContent)
            return;

        float[] widths = Widths(availableSpace.Width, context.Planning);
        bool[] finished = Finished();
        bool rightToLeft = (ReadingDirection ?? context.Planning.ReadingDirection) == Pdf.ReadingDirection.RightToLeft;
        float along = OnGrid ? LeadingGridColumns * (GridColumn(availableSpace.Width) + Gutter) : 0f;

        for (int index = 0; index < Items.Count; index++)
        {
            ColumnSlot item = Items[index];
            float width = widths[index];

            // Content drawn again on every page is drawn beside the rest, finished or not; it has no say in the
            // row's height, which was planned without it.
            if (!finished[index] || item.Repeats)
            {
                Fit plan = item.Plan(new Extent(width, availableSpace.Height), context.Planning);

                if (plan.PlacesContent)
                {
                    Offset left = new Offset(rightToLeft ? availableSpace.Width - along - width : along, 0);
                    context.Surface.MoveOrigin(left);
                    context.RenderAllotted(item, new Extent(width, row.Size.Height), availableSpace.Height);
                    context.Surface.MoveOrigin(left.Reverse());
                }

                if (plan.IsComplete || plan.IsNothing)
                    finished[index] = true;
            }

            // Every item keeps its place across the row, finished or not, so the ones still running do not move.
            along += width + Gutter;
        }
    }

    /// <summary>The flags of which items are finished, made afresh for a row whose items have changed.</summary>
    private bool[] Finished()
    {
        if (_finished.Length != Items.Count)
            _finished = new bool[Items.Count];

        return _finished;
    }

    private float FixedWidth()
    {
        float total = 0f;

        foreach (ColumnSlot item in Items)
        {
            if (item.Sizing == ColumnSizing.Fixed)
                total += Math.Max(0f, item.Value);
        }

        return total;
    }

    private float GridColumn(float width) => (width - Gutter * (GridColumns - 1)) / GridColumns;

    private float[] Widths(float width, PlanContext context)
    {
        if (OnGrid)
        {
            float column = GridColumn(width);
            float[] spans = new float[Items.Count];

            for (int index = 0; index < Items.Count; index++)
            {
                float span = Items[index].Value;
                spans[index] = Math.Max(0f, span * column + (span - 1) * Gutter);
            }

            return spans;
        }

        if (_widths is { } known && known.Length == Items.Count && Math.Abs(_widthsFor - width) <= Extent.Epsilon)
            return known;

        float[] widths = new float[Items.Count];
        float left = Math.Max(0f, width - Gutter * (Items.Count - 1)) - FixedWidth();
        float weights = 0f;
        bool settled = true;

        for (int index = 0; index < Items.Count; index++)
        {
            ColumnSlot item = Items[index];

            switch (item.Sizing)
            {
                case ColumnSizing.Fixed:
                    widths[index] = Math.Max(0f, item.Value);
                    break;

                case ColumnSizing.Natural:
                    // Measured without a limit on height, so the width is that of all the content, not only of the
                    // part that fits on this page.
                    float room = Math.Max(0f, left);
                    Fit measured = item.Plan(new Extent(room, Extent.Max.Height), context);

                    if (measured.IsDeferred)
                    {
                        // Too wide for now; the room may be larger on a later page, so the answer is not kept.
                        settled = false;
                        break;
                    }

                    widths[index] = Math.Min(measured.Size.Width, room);
                    left -= widths[index];
                    break;

                case ColumnSizing.Share:
                    weights += Math.Max(0f, item.Value);
                    break;
            }
        }

        if (weights > 0)
        {
            float shared = Math.Max(0f, left);

            for (int index = 0; index < Items.Count; index++)
            {
                if (Items[index].Sizing == ColumnSizing.Share)
                    widths[index] = shared * Math.Max(0f, Items[index].Value) / weights;
            }
        }

        if (settled)
        {
            _widths = widths;
            _widthsFor = width;
        }

        return widths;
    }

    private sealed record Saved(bool[] Finished, float[]? Widths, float WidthsFor);
}
