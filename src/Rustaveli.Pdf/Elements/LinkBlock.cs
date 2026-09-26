using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Makes the area occupied by its child clickable, opening an external URL.
/// </summary>
public sealed class LinkBlock : EnclosingBlock
{
    public string Url { get; set; } = string.Empty;

    public override void Render(Extent availableSpace, RenderContext context)
    {
        Fit plan = Plan(availableSpace, context.Layout);

        if (plan.IsDeferred || plan.IsNothing)
            return;

        base.Render(availableSpace, context);

        if (!string.IsNullOrEmpty(Url))
            context.Canvas.DrawExternalLink(Url, availableSpace);
    }
}
