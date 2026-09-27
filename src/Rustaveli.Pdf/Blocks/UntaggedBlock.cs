using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Tagging;

namespace Rustaveli.Pdf.Blocks;

/// <summary>Draws its content as decoration, outside the document's structure: read by no screen reader.</summary>
internal sealed class UntaggedBlock : EnclosingBlock
{
    protected override void RenderCore(Extent availableSpace, RenderContext context)
    {
        using TagStack.Scope scope = context.Tags.Untag();
        base.RenderCore(availableSpace, context);
    }
}
