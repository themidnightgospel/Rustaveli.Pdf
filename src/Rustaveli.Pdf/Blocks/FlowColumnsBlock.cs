using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Flows a story down one column and on into the next, as a newspaper sets it, and on to the next page when the
/// columns are full; balanced, the columns of its last page end level.
/// </summary>
/// <remarks>
/// Measuring cannot change where the story has got to, so it draws the story ahead onto a surface that keeps
/// nothing, column by column, to see where it would end, then returns the story to where it was.
/// </remarks>
internal sealed class FlowColumnsBlock : Block
{
    /// <summary>Balancing narrows in on the shortest height this many times, to well under a point on any page.</summary>
    private const int BalancingSteps = 16;

    public int Count { get; set; } = 2;

    public float Gutter { get; set; }

    /// <summary>Whether the columns of the last page end level rather than the first filling before the next.</summary>
    public bool Balanced { get; set; }

    /// <summary>The content flowing through the columns.</summary>
    public Block? Story { get; set; }

    /// <summary>Drawn in each gutter between columns in use, as tall as the columns: a rule, say.</summary>
    public Block? Between { get; set; }

    public override IEnumerable<Block?> GetChildren()
    {
        yield return Story;
        yield return Between;
    }

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        if (Story is null)
            return Fit.Complete(Extent.Zero);

        float width = ColumnWidth(availableSpace.Width);

        if (width <= 0)
            return Fit.Defer($"{Count} columns with gutters of {Gutter:F1} leave no width in {availableSpace.Width:F1} points.");

        Pour pour = Measure(width, availableSpace.Height, context);

        if (pour.Columns == 0)
        {
            return pour.Done
                ? Fit.Nothing()
                : Fit.Defer($"The story does not fit even one column {width:F1} by {availableSpace.Height:F1} points.");
        }

        Extent size = new Extent(availableSpace.Width, pour.Height);
        return pour.Done ? Fit.Complete(size) : Fit.Partial(size);
    }

    public override void Render(Extent availableSpace, RenderContext context)
    {
        if (Story is null)
            return;

        float width = ColumnWidth(availableSpace.Width);

        if (width <= 0)
            return;

        Pour measured = Measure(width, availableSpace.Height, context.Planning);

        if (measured.Columns == 0)
            return;

        ISurface surface = context.Surface;
        bool rightToLeft = context.Planning.ReadingDirection == ReadingDirection.RightToLeft;

        Pour drawn = Flow(width, measured.ColumnHeight, context.Planning, context, column =>
        {
            float x = column * (width + Gutter);
            return new Offset(rightToLeft ? availableSpace.Width - x - width : x, 0);
        });

        if (Between is null || Gutter <= 0)
            return;

        for (int gap = 0; gap < drawn.Columns - 1; gap++)
        {
            float x = ((gap + 1) * (width + Gutter)) - Gutter;
            Offset at = new Offset(rightToLeft ? availableSpace.Width - x - Gutter : x, 0);

            // Drawn afresh in every gutter, as a layer is on every page.
            Between.ResetState(includeDocumentProgress: false);
            surface.Translate(at);
            Between.Render(new Extent(Gutter, drawn.Height), context);
            surface.Translate(at.Reverse());
        }
    }

    private float ColumnWidth(float available) => (available - (Gutter * (Count - 1))) / Count;

    /// <summary>
    /// Where the story would end in columns <paramref name="width"/> wide and at most <paramref name="height"/> tall,
    /// leaving it where it was. Balanced, the story is poured into the shortest columns it fits whenever it ends on
    /// this page.
    /// </summary>
    private Pour Measure(float width, float height, PlanContext context)
    {
        using CountingPageSink nowhere = new CountingPageSink();
        RenderContext ahead = new RenderContext(nowhere, context);

        Pour full = Trial(height);

        if (!Balanced || !full.Done || Count < 2)
            return full;

        // The shortest height in which the story still ends within the columns, narrowed in on by halves.
        float low = 0f;
        float high = full.Height;
        Pour best = full;

        for (int step = 0; step < BalancingSteps; step++)
        {
            float middle = (low + high) / 2;
            Pour trial = Trial(middle);

            if (trial.Done && trial.Columns > 0)
            {
                high = middle;
                best = trial;
            }
            else
            {
                low = middle;
            }
        }

        return best;

        Pour Trial(float columnHeight)
        {
            Progress saved = Story!.SaveProgress();

            try
            {
                return Flow(width, columnHeight, context, ahead, static _ => Offset.Zero);
            }
            finally
            {
                Story.RestoreProgress(saved);
            }
        }
    }

    /// <summary>
    /// Draws the story into columns <paramref name="width"/> wide and <paramref name="height"/> tall, each placed at
    /// the offset <paramref name="place"/> gives, until it ends or the columns are full.
    /// </summary>
    private Pour Flow(float width, float height, PlanContext planning, RenderContext render, Func<int, Offset> place)
    {
        Extent column = new Extent(width, height);
        float tallest = 0f;

        for (int index = 0; index < Count; index++)
        {
            Fit plan = Story!.Plan(column, planning);

            if (plan.IsNothing)
                return new Pour(index, tallest, height, Done: true);

            if (plan.IsDeferred)
                return new Pour(index, tallest, height, Done: false);

            tallest = Math.Max(tallest, plan.Size.Height);

            Offset at = place(index);
            render.Surface.Translate(at);
            Story.Render(column, render);
            render.Surface.Translate(at.Reverse());

            if (plan.IsComplete)
                return new Pour(index + 1, tallest, height, Done: true);
        }

        return new Pour(Count, tallest, height, Done: false);
    }

    /// <summary>How many columns the story took, the tallest of them, the height each was given, and whether it ended.</summary>
    private readonly record struct Pour(int Columns, float Height, float ColumnHeight, bool Done);
}
