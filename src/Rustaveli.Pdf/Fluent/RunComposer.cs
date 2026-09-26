using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Fluent;

/// <summary>
/// Refines the style of a single span.
/// </summary>
/// <remarks>
/// Each call composes another transformation on top of the inherited style rather than replacing it, so
/// <c>.Bold().FontSize(18)</c> applies both and still inherits the typeface from its surroundings.
/// </remarks>
public sealed class RunComposer(TextRun span)
{
    private RunComposer Refine(Func<TypeStyle, TypeStyle> refinement)
    {
        Func<TypeStyle, TypeStyle>? previous = span.StyleOverride;

        span.StyleOverride = previous is null
            ? refinement
            : style => refinement(previous(style));

        return this;
    }

    public RunComposer FontFamily(string fontFamily) => Refine(style => style.FontFamilyOf(fontFamily));

    public RunComposer FontSize(float size) => Refine(style => style.FontSizeOf(size));

    public RunComposer FontColor(Ink color) => Refine(style => style.ColorOf(color));

    public RunComposer FontColor(string hexColor) => FontColor(Ink.Hex(hexColor));

    public RunComposer BackgroundColor(Ink color) => Refine(style => style.BackgroundColorOf(color));

    public RunComposer BackgroundColor(string hexColor) => BackgroundColor(Ink.Hex(hexColor));

    public RunComposer Weight(TypeWeight weight) => Refine(style => style.WeightOf(weight));

    public RunComposer Bold() => Refine(style => style.Bold());

    public RunComposer Italic(bool value = true) => Refine(style => style.Italic(value));

    public RunComposer Underline(bool value = true) => Refine(style => style.Underline(value));

    public RunComposer Strikethrough(bool value = true) => Refine(style => style.Strikethrough(value));

    public RunComposer LineHeight(float multiplier) => Refine(style => style.LineHeightOf(multiplier));

    public RunComposer LetterSpacing(float spacing) => Refine(style => style.LetterSpacingOf(spacing));

    public RunComposer Subscript() => Refine(style => style.Subscript());

    public RunComposer Superscript() => Refine(style => style.Superscript());

    /// <summary>Applies an arbitrary style transformation.</summary>
    public RunComposer Style(Func<TypeStyle, TypeStyle> refinement) => Refine(refinement);
}
