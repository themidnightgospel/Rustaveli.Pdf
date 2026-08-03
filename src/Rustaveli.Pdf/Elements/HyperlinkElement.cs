using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Primitives;

namespace Rustaveli.Pdf.Elements;

/// <summary>
/// Makes the area occupied by its child clickable, opening an external URL.
/// </summary>
public sealed class HyperlinkElement : ContainerElement
{
    public string Url { get; set; } = string.Empty;

    public override void Draw(Size availableSpace, DrawContext context)
    {
        SpacePlan plan = Measure(availableSpace, context.Layout);

        if (plan.IsWrap || plan.IsEmpty)
            return;

        base.Draw(availableSpace, context);

        if (!string.IsNullOrEmpty(Url))
            context.Canvas.DrawExternalLink(Url, plan.Size);
    }
}
