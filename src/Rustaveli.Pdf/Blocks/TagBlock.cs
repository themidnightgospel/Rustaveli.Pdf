using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Tagging;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Makes its content one element of the document's structure: created where the content is first drawn, and the same
/// element on every page the content goes on to cover.
/// </summary>
internal sealed class TagBlock : EnclosingBlock
{
    private StructureElement? _element;

    public required ContentTag Tag { get; init; }

    // The element is who the content is, not how far it has got, so a saved and restored plan keeps it.
    protected override void ResetOwnState() => _element = null;

    protected override object? SaveOwnProgress() => null;

    protected override void RestoreOwnProgress(object progress)
    {
    }

    public override void Render(Extent availableSpace, RenderContext context)
    {
        if (context.Tags.Enabled && _element is null && Plan(availableSpace, context.Planning) is { IsDeferred: false, IsNothing: false })
            _element = context.Tags.Create(Tag);

        using TagStack.Scope scope = context.Tags.Enter(_element);
        base.Render(availableSpace, context);
    }
}
