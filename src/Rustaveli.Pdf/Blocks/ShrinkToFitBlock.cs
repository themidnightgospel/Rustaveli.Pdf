using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Draws its child scaled down evenly, just far enough for the whole of it to fit the room it is given.
/// </summary>
/// <remarks>
/// The scale cannot be worked out from the child's size, because content need not grow in proportion to its room:
/// text given a wider line breaks into fewer lines and gets shorter. So the scale is searched for, by halving an interval known to
/// hold the answer, asking the child at each step whether it fits.
/// </remarks>
internal sealed class ShrinkToFitBlock : EnclosingBlock
{
    /// <summary>
    /// The floor used when <see cref="MinScale"/> sets none. It stands for "no floor at all": content shrunk further
    /// than this would be too small to read, so no real content is refused for it, while the search still starts from
    /// a scale content can be planned at.
    /// </summary>
    private const float NoFloor = 1f / 128;

    /// <summary>
    /// How close to the largest fitting scale the search comes before it stops, as a share of that scale: close
    /// enough to be invisible on the page, and few enough steps that text is not laid out needlessly often.
    /// </summary>
    private const float Precision = 1f / 1024;

    /// <summary>
    /// The smallest scale the content may be drawn at, a quarter unless set. Zero, a negative number or NaN sets no
    /// floor; one or more means the content is never shrunk.
    /// </summary>
    public float MinScale { get; set; } = 0.25f;

    protected override Fit PlanCore(Extent availableSpace, PlanContext context)
    {
        if (Child is null)
            return Fit.Complete(Extent.Zero);

        if (ChooseScale(availableSpace, context) is not { } scale)
        {
            // Content that fits at no scale is left to itself: content that splits goes on paginating as it would,
            // rather than being refused outright.
            return Child.Plan(availableSpace, context);
        }

        // Asked again at the scale chosen, content may answer differently; its answer is passed on as it is, so
        // content that no longer fits moves on instead of passing for content of no size.
        Fit plan = Child.Plan(Divide(availableSpace, scale), context);

        return plan.PlacesContent
            ? Fit.Complete(plan.Size.Width * scale, plan.Size.Height * scale)
            : plan;
    }

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (Child is null)
            return;

        if (ChooseScale(availableSpace, context.Planning) is not { } scale || scale >= 1f)
        {
            Child.Render(availableSpace, context);
            return;
        }

        context.Surface.Save();
        context.Surface.ScaleAxes(scale, scale);
        Child.Render(Divide(availableSpace, scale), context);
        context.Surface.Restore();
    }

    /// <summary>
    /// The largest scale, down to the floor, at which the child fits whole — within <see cref="Precision"/> of it, and
    /// never above it — or null when there is none.
    /// </summary>
    private float? ChooseScale(Extent room, PlanContext context)
    {
        if (FitsAt(1f, room, context))
            return 1f;

        float floor = MinScale > 0 ? MinScale : NoFloor;

        if (floor >= 1f || !FitsAt(floor, room, context))
            return null;

        // The child fits at the low end and not at the high end; each step keeps that true of a half as wide.
        float fits = floor;
        float fails = 1f;

        while (fails - fits > fails * Precision)
        {
            float middle = (fits + fails) / 2;

            if (FitsAt(middle, room, context))
                fits = middle;
            else
                fails = middle;
        }

        return fits;
    }

    private bool FitsAt(float scale, Extent room, PlanContext context)
    {
        Fit plan = Child!.Plan(Divide(room, scale), context);
        return plan.IsComplete || plan.IsNothing;
    }

    private static Extent Divide(Extent room, float scale) => new Extent(room.Width / scale, room.Height / scale);
}
