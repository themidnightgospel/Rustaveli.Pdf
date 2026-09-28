using Rustaveli.Pdf.Layout;
using Rustaveli.Pdf.Tagging;

namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// Sets the language its content is in, for screen readers to pronounce it by. Elements created inside carry it;
/// inside an element that holds text itself, such as a paragraph, the content becomes a span in that language.
/// </summary>
internal sealed class LanguageBlock : EnclosingBlock
{
    private StructureElement? _span;

    public required string Language { get; init; }

    protected override void ResetOwnState() => _span = null;

    protected override object? SaveOwnProgress() => null;

    protected override void RestoreOwnProgress(object progress)
    {
    }

    public override void Render(Extent availableSpace, RenderContext context)
    {
        TagStack tags = context.Tags;

        if (tags.Enabled && _span is null && !tags.Current.IsGrouping && Plan(availableSpace, context.Planning) is { IsDeferred: false, IsNothing: false })
        {
            _span = tags.Create("Span");
            _span?.Language = Language;
        }

        using TagStack.Scope inside = tags.Enter(_span);
        using TagStack.Scope speaking = tags.Speak(Language);
        base.Render(availableSpace, context);
    }
}
