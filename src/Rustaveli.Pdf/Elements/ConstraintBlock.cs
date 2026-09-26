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
public sealed class ConstraintBlock : EnclosingBlock
{
    public float? MinWidth { get; set; }

    public float? MaxWidth { get; set; }

    public float? MinHeight { get; set; }

    public float? MaxHeight { get; set; }

    public override Fit Plan(Extent availableSpace, PlanContext context)
    {
        if (MinWidth > availableSpace.Width + Extent.Epsilon)
            return Fit.Defer($"The requested minimum width ({MinWidth:F1}) exceeds the available width ({availableSpace.Width:F1}).");

        if (MinHeight > availableSpace.Height + Extent.Epsilon)
            return Fit.Defer($"The requested minimum height ({MinHeight:F1}) exceeds the available height ({availableSpace.Height:F1}).");

        Extent innerSpace = new Extent(
            Math.Min(availableSpace.Width, MaxWidth ?? availableSpace.Width),
            Math.Min(availableSpace.Height, MaxHeight ?? availableSpace.Height));

        Fit childPlan = Child?.Plan(innerSpace, context) ?? Fit.Complete(Extent.Zero);

        if (childPlan.IsDeferred)
            return childPlan;

        if (childPlan.IsNothing)
            return Fit.Nothing();

        // Grow to the minimum, but never past what the parent offered.
        Extent size = new Extent(
            Math.Min(Math.Max(childPlan.Size.Width, MinWidth ?? 0), availableSpace.Width),
            Math.Min(Math.Max(childPlan.Size.Height, MinHeight ?? 0), availableSpace.Height));

        return childPlan.IsComplete ? Fit.Complete(size) : Fit.Partial(size);
    }

    public override void Render(Extent availableSpace, RenderContext context)
    {
        if (Child is null)
            return;

        Extent innerSpace = new Extent(
            Math.Min(availableSpace.Width, MaxWidth ?? availableSpace.Width),
            Math.Min(availableSpace.Height, MaxHeight ?? availableSpace.Height));

        Child.Render(innerSpace, context);
    }
}
