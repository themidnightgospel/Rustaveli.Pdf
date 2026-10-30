using Rustaveli.Pdf.Drawing;
using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Tagging;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Makes the area occupied by its child clickable, going wherever the kind of link says.
/// </summary>
internal abstract class LinkingBlock : EnclosingBlock
{
    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        if (!Plan(availableSpace, context.Planning).PlacesContent)
            return;

        // The content is a link in the structure, which the link itself belongs to.
        using TagStack.Scope scope = context.Tags.Enter(context.Tags.Create("Link"));
        base.RenderCore(availableSpace, context);

        AddLink(context.Surface, availableSpace);
    }

    /// <summary>Makes <paramref name="area"/> a link, unless the link has nowhere to go.</summary>
    protected abstract void AddLink(ISurface surface, Extent area);
}
