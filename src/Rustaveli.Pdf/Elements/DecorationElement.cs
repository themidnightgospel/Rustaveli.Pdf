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
public sealed class DecorationElement : Element
{
    public Container Before { get; } = new();

    public Container Content { get; } = new();

    public Container After { get; } = new();

    public override IEnumerable<Element?> GetChildren()
    {
        yield return Before;
        yield return Content;
        yield return After;
    }

    public override SpacePlan Measure(Size availableSpace, LayoutContext context)
    {
        (Size Before, Size After)? bands = MeasureBands(availableSpace, context);

        if (bands is null)
            return SpacePlan.Wrap("The available space is not sufficient for the decoration bands.");

        (Size beforeSize, Size afterSize) = bands.Value;
        float contentHeight = availableSpace.Height - beforeSize.Height - afterSize.Height;

        if (contentHeight < -Size.Epsilon)
            return SpacePlan.Wrap("The decoration bands leave no room for the content.");

        SpacePlan contentPlan = Content.Measure(new Size(availableSpace.Width, contentHeight), context);

        if (contentPlan.IsWrap)
            return contentPlan;

        if (contentPlan.IsEmpty)
            return SpacePlan.Empty();

        Size size = new Size(
            Math.Max(contentPlan.Size.Width, Math.Max(beforeSize.Width, afterSize.Width)),
            beforeSize.Height + contentPlan.Size.Height + afterSize.Height);

        return contentPlan.IsFullRender ? SpacePlan.FullRender(size) : SpacePlan.PartialRender(size);
    }

    public override void Draw(Size availableSpace, DrawContext context)
    {
        (Size Before, Size After)? bands = MeasureBands(availableSpace, context.Layout);

        if (bands is null)
            return;

        (Size beforeSize, Size afterSize) = bands.Value;
        float contentHeight = availableSpace.Height - beforeSize.Height - afterSize.Height;

        if (contentHeight < -Size.Epsilon)
            return;

        SpacePlan contentPlan = Content.Measure(new Size(availableSpace.Width, contentHeight), context.Layout);

        if (contentPlan.IsWrap || contentPlan.IsEmpty)
            return;

        ICanvas canvas = context.Canvas;

        Before.Draw(new Size(availableSpace.Width, beforeSize.Height), context);

        canvas.Translate(new Position(0, beforeSize.Height));
        Content.Draw(new Size(availableSpace.Width, contentHeight), context);
        canvas.Translate(new Position(0, -beforeSize.Height));

        float afterTop = beforeSize.Height + contentPlan.Size.Height;
        canvas.Translate(new Position(0, afterTop));
        After.Draw(new Size(availableSpace.Width, afterSize.Height), context);
        canvas.Translate(new Position(0, -afterTop));

        // The bands repeat on every page, but their content tracks how much of itself it has drawn and would
        // report nothing left next time. Reset after drawing so measurement stays free of side effects.
        Before.ResetState(includeDocumentProgress: false);
        After.ResetState(includeDocumentProgress: false);
    }

    private (Size Before, Size After)? MeasureBands(Size availableSpace, LayoutContext context)
    {
        SpacePlan beforePlan = Before.Measure(availableSpace, context);

        if (beforePlan.IsWrap)
            return null;

        Size remaining = new Size(availableSpace.Width, availableSpace.Height - beforePlan.Size.Height);

        if (remaining.IsNegative)
            return null;

        SpacePlan afterPlan = After.Measure(remaining, context);

        if (afterPlan.IsWrap)
            return null;

        return (beforePlan.Size, afterPlan.Size);
    }
}
