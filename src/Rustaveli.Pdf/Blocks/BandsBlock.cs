using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Sandwiches paginated content between two fixed bands that repeat on every page.
/// </summary>
/// <remarks>
/// Only the middle section flows; the bands are re-drawn in full each time. This is the building block for
/// repeating captions and section headers that must accompany content wherever it breaks.
/// </remarks>
internal sealed class BandsBlock : Block
{
    public Frame Head { get; } = new();

    public Frame Body { get; } = new();

    public Frame Foot { get; } = new();

    public override IEnumerable<Block?> GetChildren()
    {
        yield return Head;
        yield return Body;
        yield return Foot;
    }

    public override Fit Plan(Extent availableSpace, PlanContext context)
    {
        (Extent Before, Extent After)? bands = MeasureBands(availableSpace, context);

        if (bands is null)
            return Fit.Defer("The space available is too small for the head and foot bands.");

        (Extent beforeSize, Extent afterSize) = bands.Value;
        float contentHeight = availableSpace.Height - beforeSize.Height - afterSize.Height;

        if (contentHeight < -Extent.Epsilon)
            return Fit.Defer("The head and foot bands leave no room for the body.");

        Fit contentPlan = Body.Plan(new Extent(availableSpace.Width, contentHeight), context);

        if (contentPlan.IsDeferred)
            return contentPlan;

        if (contentPlan.IsNothing)
            return Fit.Nothing();

        Extent size = new Extent(
            Math.Max(contentPlan.Size.Width, Math.Max(beforeSize.Width, afterSize.Width)),
            beforeSize.Height + contentPlan.Size.Height + afterSize.Height);

        return contentPlan.IsComplete ? Fit.Complete(size) : Fit.Partial(size);
    }

    public override void Render(Extent availableSpace, RenderContext context)
    {
        (Extent Before, Extent After)? bands = MeasureBands(availableSpace, context.Planning);

        if (bands is null)
            return;

        (Extent beforeSize, Extent afterSize) = bands.Value;
        float contentHeight = availableSpace.Height - beforeSize.Height - afterSize.Height;

        if (contentHeight < -Extent.Epsilon)
            return;

        Fit contentPlan = Body.Plan(new Extent(availableSpace.Width, contentHeight), context.Planning);

        if (contentPlan.IsDeferred || contentPlan.IsNothing)
            return;

        ISurface canvas = context.Surface;

        Head.Render(new Extent(availableSpace.Width, beforeSize.Height), context);

        canvas.Translate(new Offset(0, beforeSize.Height));
        Body.Render(new Extent(availableSpace.Width, contentHeight), context);
        canvas.Translate(new Offset(0, -beforeSize.Height));

        float afterTop = beforeSize.Height + contentPlan.Size.Height;
        canvas.Translate(new Offset(0, afterTop));
        Foot.Render(new Extent(availableSpace.Width, afterSize.Height), context);
        canvas.Translate(new Offset(0, -afterTop));

        // The bands repeat on every page, but their content tracks how much of itself it has drawn and would
        // report nothing left next time. Reset after drawing so measurement stays free of side effects.
        Head.ResetState(includeDocumentProgress: false);
        Foot.ResetState(includeDocumentProgress: false);
    }

    private (Extent Before, Extent After)? MeasureBands(Extent availableSpace, PlanContext context)
    {
        Fit beforePlan = Head.Plan(availableSpace, context);

        if (beforePlan.IsDeferred)
            return null;

        Extent remaining = new Extent(availableSpace.Width, availableSpace.Height - beforePlan.Size.Height);

        if (remaining.IsNegative)
            return null;

        Fit afterPlan = Foot.Plan(remaining, context);

        if (afterPlan.IsDeferred)
            return null;

        return (beforePlan.Size, afterPlan.Size);
    }
}
