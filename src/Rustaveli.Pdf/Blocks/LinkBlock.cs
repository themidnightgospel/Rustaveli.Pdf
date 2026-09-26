using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Makes the area occupied by its child clickable, opening an external URL.
/// </summary>
internal sealed class LinkBlock : EnclosingBlock
{
    public string Url { get; set; } = string.Empty;

    public override void Render(Extent availableSpace, RenderContext context)
    {
        Fit plan = Plan(availableSpace, context.Planning);

        if (plan.IsDeferred || plan.IsNothing)
            return;

        base.Render(availableSpace, context);

        if (!string.IsNullOrEmpty(Url))
            context.Surface.DrawExternalLink(Url, availableSpace);
    }
}
