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
public sealed class TextSpanDescriptor(TextSpan span)
{
    private TextSpanDescriptor Refine(Func<TextStyle, TextStyle> refinement)
    {
        Func<TextStyle, TextStyle>? previous = span.StyleOverride;

        span.StyleOverride = previous is null
            ? refinement
            : style => refinement(previous(style));

        return this;
    }

    public TextSpanDescriptor FontFamily(string fontFamily) => Refine(style => style.FontFamilyOf(fontFamily));

    public TextSpanDescriptor FontSize(float size) => Refine(style => style.FontSizeOf(size));

    public TextSpanDescriptor FontColor(Ink color) => Refine(style => style.ColorOf(color));

    public TextSpanDescriptor FontColor(string hexColor) => FontColor(Ink.Hex(hexColor));

    public TextSpanDescriptor BackgroundColor(Ink color) => Refine(style => style.BackgroundColorOf(color));

    public TextSpanDescriptor BackgroundColor(string hexColor) => BackgroundColor(Ink.Hex(hexColor));

    public TextSpanDescriptor Weight(FontWeight weight) => Refine(style => style.WeightOf(weight));

    public TextSpanDescriptor Bold() => Refine(style => style.Bold());

    public TextSpanDescriptor Italic(bool value = true) => Refine(style => style.Italic(value));

    public TextSpanDescriptor Underline(bool value = true) => Refine(style => style.Underline(value));

    public TextSpanDescriptor Strikethrough(bool value = true) => Refine(style => style.Strikethrough(value));

    public TextSpanDescriptor LineHeight(float multiplier) => Refine(style => style.LineHeightOf(multiplier));

    public TextSpanDescriptor LetterSpacing(float spacing) => Refine(style => style.LetterSpacingOf(spacing));

    public TextSpanDescriptor Subscript() => Refine(style => style.Subscript());

    public TextSpanDescriptor Superscript() => Refine(style => style.Superscript());

    /// <summary>Applies an arbitrary style transformation.</summary>
    public TextSpanDescriptor Style(Func<TextStyle, TextStyle> refinement) => Refine(refinement);
}
