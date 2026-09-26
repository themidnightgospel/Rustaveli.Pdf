using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Forces its child into a fixed width-to-height ratio.
/// </summary>
public sealed class ProportionBlock : EnclosingBlock
{
    /// <summary>Width divided by height. Must be greater than zero.</summary>
    public float Ratio { get; set; } = 1f;

    public ProportionFit Option { get; set; } = ProportionFit.FitWidth;

    public override Fit Measure(Extent availableSpace, PlanContext context)
    {
        if (Ratio <= 0)
            return Fit.Wrap("The aspect ratio must be greater than zero.");

        Extent size = ResolveSize(availableSpace);

        if (!size.FitsIn(availableSpace))
            return Fit.Wrap("The available space is too small for the requested aspect ratio.");

        Fit childPlan = Child?.Measure(size, context) ?? Fit.FullRender(Extent.Zero);

        if (childPlan.IsWrap)
            return childPlan;

        if (childPlan.IsEmpty)
            return Fit.Empty();

        return childPlan.IsFullRender ? Fit.FullRender(size) : Fit.PartialRender(size);
    }

    public override void Draw(Extent availableSpace, RenderContext context) =>
        Child?.Draw(ResolveSize(availableSpace), context);

    private Extent ResolveSize(Extent availableSpace)
    {
        Extent fromWidth = new Extent(availableSpace.Width, availableSpace.Width / Ratio);
        Extent fromHeight = new Extent(availableSpace.Height * Ratio, availableSpace.Height);

        return Option switch
        {
            ProportionFit.FitWidth => fromWidth,
            ProportionFit.FitHeight => fromHeight,
            // Pick whichever axis binds first so the result stays inside the offered space.
            ProportionFit.FitArea => fromWidth.Height <= availableSpace.Height ? fromWidth : fromHeight,
            _ => fromWidth
        };
    }
}
