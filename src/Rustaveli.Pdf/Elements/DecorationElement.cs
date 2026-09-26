using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Sandwiches paginated content between two fixed bands that repeat on every page.
/// </summary>
/// <remarks>
/// Only the middle section flows; the bands are re-drawn in full each time. This is the building block for
/// repeating captions and section headers that must accompany content wherever it breaks.
/// </remarks>
public sealed class DecorationElement : Block
{
    public Frame Before { get; } = new();

    public Frame Content { get; } = new();

    public Frame After { get; } = new();

    public override IEnumerable<Block?> GetChildren()
    {
        yield return Before;
        yield return Content;
        yield return After;
    }

    public override Fit Measure(Extent availableSpace, PlanContext context)
    {
        (Extent Before, Extent After)? bands = MeasureBands(availableSpace, context);

        if (bands is null)
            return Fit.Wrap("The available space is not sufficient for the decoration bands.");

        (Extent beforeSize, Extent afterSize) = bands.Value;
        float contentHeight = availableSpace.Height - beforeSize.Height - afterSize.Height;

        if (contentHeight < -Extent.Epsilon)
            return Fit.Wrap("The decoration bands leave no room for the content.");

        Fit contentPlan = Content.Measure(new Extent(availableSpace.Width, contentHeight), context);

        if (contentPlan.IsWrap)
            return contentPlan;

        if (contentPlan.IsEmpty)
            return Fit.Empty();

        Extent size = new Extent(
            Math.Max(contentPlan.Size.Width, Math.Max(beforeSize.Width, afterSize.Width)),
            beforeSize.Height + contentPlan.Size.Height + afterSize.Height);

        return contentPlan.IsFullRender ? Fit.FullRender(size) : Fit.PartialRender(size);
    }

    public override void Draw(Extent availableSpace, RenderContext context)
    {
        (Extent Before, Extent After)? bands = MeasureBands(availableSpace, context.Layout);

        if (bands is null)
            return;

        (Extent beforeSize, Extent afterSize) = bands.Value;
        float contentHeight = availableSpace.Height - beforeSize.Height - afterSize.Height;

        if (contentHeight < -Extent.Epsilon)
            return;

        Fit contentPlan = Content.Measure(new Extent(availableSpace.Width, contentHeight), context.Layout);

        if (contentPlan.IsWrap || contentPlan.IsEmpty)
            return;

        ISurface canvas = context.Canvas;

        Before.Draw(new Extent(availableSpace.Width, beforeSize.Height), context);

        canvas.Translate(new Offset(0, beforeSize.Height));
        Content.Draw(new Extent(availableSpace.Width, contentHeight), context);
        canvas.Translate(new Offset(0, -beforeSize.Height));

        float afterTop = beforeSize.Height + contentPlan.Size.Height;
        canvas.Translate(new Offset(0, afterTop));
        After.Draw(new Extent(availableSpace.Width, afterSize.Height), context);
        canvas.Translate(new Offset(0, -afterTop));

        // The bands repeat on every page, but their content tracks how much of itself it has drawn and would
        // report nothing left next time. Reset after drawing so measurement stays free of side effects.
        Before.ResetState(includeDocumentProgress: false);
        After.ResetState(includeDocumentProgress: false);
    }

    private (Extent Before, Extent After)? MeasureBands(Extent availableSpace, PlanContext context)
    {
        Fit beforePlan = Before.Measure(availableSpace, context);

        if (beforePlan.IsWrap)
            return null;

        Extent remaining = new Extent(availableSpace.Width, availableSpace.Height - beforePlan.Size.Height);

        if (remaining.IsNegative)
            return null;

        Fit afterPlan = After.Measure(remaining, context);

        if (afterPlan.IsWrap)
            return null;

        return (beforePlan.Size, afterPlan.Size);
    }
}
