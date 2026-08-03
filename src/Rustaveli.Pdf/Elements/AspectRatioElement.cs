using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Forces its child into a fixed width-to-height ratio.
/// </summary>
public sealed class AspectRatioElement : ContainerElement
{
    /// <summary>Width divided by height. Must be greater than zero.</summary>
    public float Ratio { get; set; } = 1f;

    public AspectRatioOption Option { get; set; } = AspectRatioOption.FitWidth;

    public override SpacePlan Measure(Size availableSpace, LayoutContext context)
    {
        if (Ratio <= 0)
            return SpacePlan.Wrap("The aspect ratio must be greater than zero.");

        Size size = ResolveSize(availableSpace);

        if (!size.FitsIn(availableSpace))
            return SpacePlan.Wrap("The available space is too small for the requested aspect ratio.");

        SpacePlan childPlan = Child?.Measure(size, context) ?? SpacePlan.FullRender(Size.Zero);

        if (childPlan.IsWrap)
            return childPlan;

        if (childPlan.IsEmpty)
            return SpacePlan.Empty();

        return childPlan.IsFullRender ? SpacePlan.FullRender(size) : SpacePlan.PartialRender(size);
    }

    public override void Draw(Size availableSpace, DrawContext context) =>
        Child?.Draw(ResolveSize(availableSpace), context);

    private Size ResolveSize(Size availableSpace)
    {
        Size fromWidth = new Size(availableSpace.Width, availableSpace.Width / Ratio);
        Size fromHeight = new Size(availableSpace.Height * Ratio, availableSpace.Height);

        return Option switch
        {
            AspectRatioOption.FitWidth => fromWidth,
            AspectRatioOption.FitHeight => fromHeight,
            // Pick whichever axis binds first so the result stays inside the offered space.
            AspectRatioOption.FitArea => fromWidth.Height <= availableSpace.Height ? fromWidth : fromHeight,
            _ => fromWidth
        };
    }
}
