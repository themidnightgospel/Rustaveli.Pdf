using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Makes the area occupied by its child clickable, jumping to a named section of the document.
/// </summary>
public sealed class InternalLinkElement : ContainerElement
{
    public string DestinationName { get; set; } = string.Empty;

    public override void Draw(Size availableSpace, DrawContext context)
    {
        SpacePlan plan = Measure(availableSpace, context.Layout);

        if (plan.IsWrap || plan.IsEmpty)
            return;

        base.Draw(availableSpace, context);

        if (!string.IsNullOrEmpty(DestinationName))
            context.Canvas.DrawInternalLink(DestinationName, plan.Size);
    }
}
