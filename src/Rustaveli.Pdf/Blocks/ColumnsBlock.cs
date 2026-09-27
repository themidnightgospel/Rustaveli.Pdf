using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Lays children out side by side, sizing each according to its <see cref="ColumnSizing" />.
/// </summary>
/// <remarks>
/// A row is as tall as its tallest item. When any item runs out of room the whole row reports a partial render,
/// and on the next page the items that already finished report themselves empty while the unfinished ones
/// continue — which is what keeps multi-column content aligned across a page break.
/// </remarks>
internal sealed class ColumnsBlock : Block
{
    private bool[]? _completed;

    private float[]? _cachedWidths;

    private float _cachedAvailableWidth = float.NaN;

    public List<ColumnSlot> Items { get; } = [];

    /// <summary>Horizontal gap inserted between consecutive items.</summary>
    public float Gutter { get; set; }

    /// <summary>
    /// When above zero, the row is a row of a grid of this many columns, and each item's value is how many of them
    /// it spans rather than a sizing of its own.
    /// </summary>
    public int GridColumns { get; set; }

    /// <summary>Overrides the inherited flow direction. Null follows the surrounding context.</summary>
    public ReadingDirection? ReadingDirection { get; set; }

    public override IEnumerable<Block?> GetChildren() => Items;

    protected override void ResetOwnState()
    {
        _completed = null;
        _cachedWidths = null;
        _cachedAvailableWidth = float.NaN;
    }

    // The completion flags change in place, so they are copied; the widths are replaced whole, never changed.
    protected override object? SaveOwnProgress() => (_completed?.ToArray(), _cachedWidths, _cachedAvailableWidth);

    protected override void RestoreOwnProgress(object progress) =>
        (_completed, _cachedWidths, _cachedAvailableWidth) = ((bool[]?, float[]?, float))progress;

    /// <summary>Lazily sizes the per-item completion flags to the current item count.</summary>
    private bool[] Completion()
    {
        if (_completed is null || _completed.Length != Items.Count)
            _completed = new bool[Items.Count];

        return _completed;
    }

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        if (Items.Count == 0)
            return Fit.Complete(Extent.Zero);

        // Constant columns cannot shrink, so a row whose fixed widths already overflow can never be laid out
        // here however much vertical room arrives. Wrapping sends it to a fresh page, where the engine's
        // non-termination guard turns a second failure into a diagnostic instead of silent overflow.
        float fixedWidth = Items.Where(item => item.Sizing == ColumnSizing.Fixed).Sum(item => Math.Max(0f, item.Value))
            + (Gutter * Math.Max(0, Items.Count - 1));

        if (fixedWidth > availableSpace.Width + Extent.Epsilon)
        {
            return Fit.Defer(
                $"The fixed columns need {fixedWidth:F1} points but only {availableSpace.Width:F1} are available.");
        }

        float[] widths = ResolveWidths(availableSpace, context);
        bool[] completed = Completion();

        float maxHeight = 0f;
        bool anyPartial = false;
        bool anyContent = false;

        for (int index = 0; index < Items.Count; index++)
        {
            if (completed[index])
                continue;

            Fit plan = Items[index].Plan(new Extent(widths[index], availableSpace.Height), context);

            if (plan.IsDeferred)
                return plan;

            if (plan.IsNothing)
                continue;

            anyContent = true;
            maxHeight = Math.Max(maxHeight, plan.Size.Height);
            anyPartial |= plan.IsPartial;
        }

        if (!anyContent)
            return Fit.Nothing();

        Extent size = new Extent(availableSpace.Width, maxHeight);

        return anyPartial ? Fit.Partial(size) : Fit.Complete(size);
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (Items.Count == 0)
            return;

        float[] widths = ResolveWidths(availableSpace, context.Planning);
        Fit plan = Plan(availableSpace, context.Planning);

        if (plan.IsDeferred || plan.IsNothing)
            return;

        // Every item is drawn against the row's own height so that cell backgrounds and borders line up
        // regardless of how much content each one holds.
        float rowHeight = plan.Size.Height;
        float offset = 0f;
        ReadingDirection direction = ReadingDirection ?? context.Planning.ReadingDirection;
        bool[] completed = Completion();

        for (int index = 0; index < Items.Count; index++)
        {
            // A repeated column finished on an earlier page is drawn again beside the columns still going, at their
            // height; it takes no part in deciding that height, so it never keeps the row going by itself.
            if (!completed[index] || Items[index].Repeats)
            {
                Fit itemPlan = Items[index].Plan(new Extent(widths[index], availableSpace.Height), context.Planning);

                if (!itemPlan.IsDeferred && !itemPlan.IsNothing)
                {
                    float position = direction == Pdf.ReadingDirection.LeftToRight
                        ? offset
                        : availableSpace.Width - offset - widths[index];

                    context.Surface.Translate(new Offset(position, 0f));
                    Items[index].Render(new Extent(widths[index], rowHeight), context);
                    context.Surface.Translate(new Offset(-position, 0f));
                }

                if (itemPlan.IsComplete || itemPlan.IsNothing)
                    completed[index] = true;
            }

            // Advanced for every item, finished or not. A column that completed on an earlier page still owns
            // its slot, and skipping it here would slide every later column left on the continuation page.
            offset += widths[index] + Gutter;
        }
    }

    /// <summary>
    /// Splits the available width across items: constants first, then measured auto items, and whatever
    /// survives is shared among the relative items by weight.
    /// </summary>
    private float[] ResolveWidths(Extent availableSpace, PlanContext context)
    {
        // Sizing an auto column means measuring its content, and both Measure and Draw need the widths on every
        // page. The result depends only on the offered width, so it is cached rather than recomputed.
        if (_cachedWidths is not null
            && _cachedWidths.Length == Items.Count
            && Math.Abs(_cachedAvailableWidth - availableSpace.Width) < Extent.Epsilon)
        {
            return _cachedWidths;
        }

        float[] widths = new float[Items.Count];

        if (GridColumns > 0)
        {
            // A cell spans whole columns of the grid and the gutters between them, whatever else shares its row, so
            // the columns line up from one row to the next.
            float column = (availableSpace.Width - (Gutter * (GridColumns - 1))) / GridColumns;

            for (int index = 0; index < Items.Count; index++)
                widths[index] = Math.Max(0f, (Items[index].Value * column) + ((Items[index].Value - 1) * Gutter));

            _cachedWidths = widths;
            _cachedAvailableWidth = availableSpace.Width;
            return widths;
        }

        float totalSpacing = Gutter * Math.Max(0, Items.Count - 1);
        float available = Math.Max(0f, availableSpace.Width - totalSpacing);
        float consumed = 0f;

        for (int index = 0; index < Items.Count; index++)
        {
            if (Items[index].Sizing != ColumnSizing.Fixed)
                continue;

            widths[index] = Math.Max(0f, Items[index].Value);
            consumed += widths[index];
        }

        for (int index = 0; index < Items.Count; index++)
        {
            if (Items[index].Sizing != ColumnSizing.Natural)
                continue;

            Extent offered = new Extent(Math.Max(0f, available - consumed), availableSpace.Height);
            Fit plan = Items[index].Plan(offered, context);

            widths[index] = plan.IsDeferred ? 0f : Math.Min(plan.Size.Width, offered.Width);
            consumed += widths[index];
        }

        float totalWeight = Items
            .Where(item => item.Sizing == ColumnSizing.Share)
            .Sum(item => Math.Max(0f, item.Value));

        if (totalWeight > 0f)
        {
            float leftover = Math.Max(0f, available - consumed);

            for (int index = 0; index < Items.Count; index++)
            {
                if (Items[index].Sizing != ColumnSizing.Share)
                    continue;

                widths[index] = leftover * Math.Max(0f, Items[index].Value) / totalWeight;
            }
        }

        _cachedWidths = widths;
        _cachedAvailableWidth = availableSpace.Width;

        return widths;
    }
}
