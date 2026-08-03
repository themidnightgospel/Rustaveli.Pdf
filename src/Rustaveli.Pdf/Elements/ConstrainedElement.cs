using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Clamps the space offered to its child, and the size reported back to its parent, between optional bounds.
/// </summary>
/// <remarks>
/// Setting a minimum and maximum to the same value pins the element to an exact size. A minimum larger than the
/// space available produces a wrap rather than an overflow, which lets the engine try again on an empty page.
/// </remarks>
public sealed class ConstrainedElement : ContainerElement
{
    public float? MinWidth { get; set; }

    public float? MaxWidth { get; set; }

    public float? MinHeight { get; set; }

    public float? MaxHeight { get; set; }

    public override SpacePlan Measure(Size availableSpace, LayoutContext context)
    {
        if (MinWidth > availableSpace.Width + Size.Epsilon)
            return SpacePlan.Wrap($"The requested minimum width ({MinWidth:F1}) exceeds the available width ({availableSpace.Width:F1}).");

        if (MinHeight > availableSpace.Height + Size.Epsilon)
            return SpacePlan.Wrap($"The requested minimum height ({MinHeight:F1}) exceeds the available height ({availableSpace.Height:F1}).");

        Size innerSpace = new Size(
            Math.Min(availableSpace.Width, MaxWidth ?? availableSpace.Width),
            Math.Min(availableSpace.Height, MaxHeight ?? availableSpace.Height));

        SpacePlan childPlan = Child?.Measure(innerSpace, context) ?? SpacePlan.FullRender(Size.Zero);

        if (childPlan.IsWrap)
            return childPlan;

        if (childPlan.IsEmpty)
            return SpacePlan.Empty();

        // Grow to the minimum, but never past what the parent offered.
        Size size = new Size(
            Math.Min(Math.Max(childPlan.Size.Width, MinWidth ?? 0), availableSpace.Width),
            Math.Min(Math.Max(childPlan.Size.Height, MinHeight ?? 0), availableSpace.Height));

        return childPlan.IsFullRender ? SpacePlan.FullRender(size) : SpacePlan.PartialRender(size);
    }

    public override void Draw(Size availableSpace, DrawContext context)
    {
        if (Child is null)
            return;

        Size innerSpace = new Size(
            Math.Min(availableSpace.Width, MaxWidth ?? availableSpace.Width),
            Math.Min(availableSpace.Height, MaxHeight ?? availableSpace.Height));

        Child.Draw(innerSpace, context);
    }
}
