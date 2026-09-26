using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Lays children out side by side, sizing each according to its <see cref="Elements.ColumnSizing" />.
/// </summary>
/// <remarks>
/// A row is as tall as its tallest item. When any item runs out of room the whole row reports a partial render,
/// and on the next page the items that already finished report themselves empty while the unfinished ones
/// continue — which is what keeps multi-column content aligned across a page break.
/// </remarks>
public sealed class ColumnsBlock : Block
{
    private bool[]? _completed;

    private float[]? _cachedWidths;

    private float _cachedAvailableWidth = float.NaN;

    public List<ColumnSlot> Items { get; } = [];

    /// <summary>Horizontal gap inserted between consecutive items.</summary>
    public float Spacing { get; set; }

    /// <summary>Overrides the inherited flow direction. Null follows the surrounding context.</summary>
    public ReadingDirection? Direction { get; set; }

    public override IEnumerable<Block?> GetChildren() => Items;

    protected override void ResetOwnState()
    {
        _completed = null;
        _cachedWidths = null;
        _cachedAvailableWidth = float.NaN;
    }

    /// <summary>Lazily sizes the per-item completion flags to the current item count.</summary>
    private bool[] Completion()
    {
        if (_completed is null || _completed.Length != Items.Count)
            _completed = new bool[Items.Count];

        return _completed;
    }

    public override Fit Measure(Extent availableSpace, PlanContext context)
    {
        if (Items.Count == 0)
            return Fit.FullRender(Extent.Zero);

        // Constant columns cannot shrink, so a row whose fixed widths already overflow can never be laid out
        // here however much vertical room arrives. Wrapping sends it to a fresh page, where the engine's
        // non-termination guard turns a second failure into a diagnostic instead of silent overflow.
        float fixedWidth = Items.Where(item => item.Sizing == ColumnSizing.Constant).Sum(item => Math.Max(0f, item.Value))
            + (Spacing * Math.Max(0, Items.Count - 1));

        if (fixedWidth > availableSpace.Width + Extent.Epsilon)
        {
            return Fit.Wrap(
                $"The row's fixed columns need {fixedWidth:F1} points but only {availableSpace.Width:F1} is available.");
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

            Fit plan = Items[index].Measure(new Extent(widths[index], availableSpace.Height), context);

            if (plan.IsWrap)
                return plan;

            if (plan.IsEmpty)
                continue;

            anyContent = true;
            maxHeight = Math.Max(maxHeight, plan.Size.Height);
            anyPartial |= plan.IsPartialRender;
        }

        if (!anyContent)
            return Fit.Empty();

        Extent size = new Extent(availableSpace.Width, maxHeight);

        return anyPartial ? Fit.PartialRender(size) : Fit.FullRender(size);
    }

    public override void Draw(Extent availableSpace, RenderContext context)
    {
        if (Items.Count == 0)
            return;

        float[] widths = ResolveWidths(availableSpace, context.Layout);
        Fit plan = Measure(availableSpace, context.Layout);

        if (plan.IsWrap || plan.IsEmpty)
            return;

        // Every item is drawn against the row's own height so that cell backgrounds and borders line up
        // regardless of how much content each one holds.
        float rowHeight = plan.Size.Height;
        float offset = 0f;
        ReadingDirection direction = Direction ?? context.Layout.ContentDirection;
        bool[] completed = Completion();

        for (int index = 0; index < Items.Count; index++)
        {
            if (!completed[index])
            {
                Fit itemPlan = Items[index].Measure(new Extent(widths[index], availableSpace.Height), context.Layout);

                if (!itemPlan.IsWrap && !itemPlan.IsEmpty)
                {
                    float position = direction == ReadingDirection.LeftToRight
                        ? offset
                        : availableSpace.Width - offset - widths[index];

                    context.Canvas.Translate(new Offset(position, 0f));
                    Items[index].Draw(new Extent(widths[index], rowHeight), context);
                    context.Canvas.Translate(new Offset(-position, 0f));
                }

                if (itemPlan.IsFullRender || itemPlan.IsEmpty)
                    completed[index] = true;
            }

            // Advanced for every item, finished or not. A column that completed on an earlier page still owns
            // its slot, and skipping it here would slide every later column left on the continuation page.
            offset += widths[index] + Spacing;
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
        float totalSpacing = Spacing * Math.Max(0, Items.Count - 1);
        float available = Math.Max(0f, availableSpace.Width - totalSpacing);
        float consumed = 0f;

        for (int index = 0; index < Items.Count; index++)
        {
            if (Items[index].Sizing != ColumnSizing.Constant)
                continue;

            widths[index] = Math.Max(0f, Items[index].Value);
            consumed += widths[index];
        }

        for (int index = 0; index < Items.Count; index++)
        {
            if (Items[index].Sizing != ColumnSizing.Auto)
                continue;

            Extent offered = new Extent(Math.Max(0f, available - consumed), availableSpace.Height);
            Fit plan = Items[index].Measure(offered, context);

            widths[index] = plan.IsWrap ? 0f : Math.Min(plan.Size.Width, offered.Width);
            consumed += widths[index];
        }

        float totalWeight = Items
            .Where(item => item.Sizing == ColumnSizing.Relative)
            .Sum(item => Math.Max(0f, item.Value));

        if (totalWeight > 0f)
        {
            float leftover = Math.Max(0f, available - consumed);

            for (int index = 0; index < Items.Count; index++)
            {
                if (Items[index].Sizing != ColumnSizing.Relative)
                    continue;

                widths[index] = leftover * Math.Max(0f, Items[index].Value) / totalWeight;
            }
        }

        _cachedWidths = widths;
        _cachedAvailableWidth = availableSpace.Width;

        return widths;
    }
}
