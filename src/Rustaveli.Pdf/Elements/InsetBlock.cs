using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Insets its child by a fixed amount on each side.
/// </summary>
public sealed class InsetBlock : EnclosingBlock
{
    public Sides Padding { get; set; } = Sides.Zero;

    public override Fit Plan(Extent availableSpace, PlanContext context)
    {
        Extent innerSpace = new Extent(
            availableSpace.Width - Padding.Horizontal,
            availableSpace.Height - Padding.Vertical);

        if (innerSpace.IsNegative)
            return Fit.Defer("The available space is smaller than the requested padding.");

        Fit childPlan = Child?.Plan(innerSpace, context) ?? Fit.Complete(Extent.Zero);

        if (childPlan.IsDeferred)
            return childPlan;

        // A child with nothing left to draw must not resurrect the padding on the next page.
        if (childPlan.IsNothing)
            return Fit.Nothing();

        Extent size = new Extent(
            childPlan.Size.Width + Padding.Horizontal,
            childPlan.Size.Height + Padding.Vertical);

        return childPlan.IsComplete ? Fit.Complete(size) : Fit.Partial(size);
    }

    public override void Render(Extent availableSpace, RenderContext context)
    {
        if (Child is null)
            return;

        Extent innerSpace = new Extent(
            availableSpace.Width - Padding.Horizontal,
            availableSpace.Height - Padding.Vertical);

        if (innerSpace.IsNegative)
            return;

        context.Canvas.Translate(new Offset(Padding.Left, Padding.Top));
        Child.Render(innerSpace, context);
        context.Canvas.Translate(new Offset(Padding.Left, Padding.Top).Reverse());
    }
}
