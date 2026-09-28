using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Tagging;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Makes the area occupied by its child clickable, jumping to a named section of the document.
/// </summary>
internal sealed class CrossReferenceBlock : EnclosingBlock
{
    public string Anchor { get; set; } = string.Empty;

    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        Fit plan = Plan(availableSpace, context.Planning);

        if (plan.IsDeferred || plan.IsNothing)
            return;

        // The content is a link in the structure, which the link itself belongs to.
        using TagStack.Scope scope = context.Tags.Enter(context.Tags.Create("Link"));
        base.RenderCore(availableSpace, context);

        if (!string.IsNullOrEmpty(Anchor))
            context.Surface.DrawInternalLink(Anchor, availableSpace);
    }
}
