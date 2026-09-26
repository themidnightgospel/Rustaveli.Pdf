using Rustaveli.Pdf.Layout;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Makes the area occupied by its child clickable, jumping to a named section of the document.
/// </summary>
internal sealed class CrossReferenceBlock : EnclosingBlock
{
    public string Anchor { get; set; } = string.Empty;

    public override void Render(Extent availableSpace, RenderContext context)
    {
        Fit plan = Plan(availableSpace, context.Planning);

        if (plan.IsDeferred || plan.IsNothing)
            return;

        base.Render(availableSpace, context);

        if (!string.IsNullOrEmpty(Anchor))
            context.Surface.DrawInternalLink(Anchor, availableSpace);
    }
}
