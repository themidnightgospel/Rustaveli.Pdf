using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Makes the area occupied by its child clickable, jumping to a named section of the document.
/// </summary>
public sealed class CrossReferenceBlock : EnclosingBlock
{
    public string DestinationName { get; set; } = string.Empty;

    public override void Render(Extent availableSpace, RenderContext context)
    {
        Fit plan = Plan(availableSpace, context.Layout);

        if (plan.IsDeferred || plan.IsNothing)
            return;

        base.Render(availableSpace, context);

        if (!string.IsNullOrEmpty(DestinationName))
            context.Canvas.DrawInternalLink(DestinationName, availableSpace);
    }
}
