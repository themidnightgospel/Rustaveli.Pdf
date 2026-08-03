using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Renders its child only if a condition holds, collapsing to nothing otherwise.
/// </summary>
public sealed class ShowIfElement : ContainerElement
{
    public bool Condition { get; set; } = true;

    public override SpacePlan Measure(Size availableSpace, LayoutContext context) =>
        Condition ? base.Measure(availableSpace, context) : SpacePlan.FullRender(Size.Zero);

    public override void Draw(Size availableSpace, DrawContext context)
    {
        if (Condition)
            base.Draw(availableSpace, context);
    }
}
