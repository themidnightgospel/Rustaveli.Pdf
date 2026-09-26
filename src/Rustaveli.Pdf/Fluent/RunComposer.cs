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

    public RunComposer Typeface(string fontFamily) => Refine(style => style.WithTypeface(fontFamily));

    public RunComposer PointSize(float size) => Refine(style => style.WithPointSize(size));

    public RunComposer Ink(Ink color) => Refine(style => style.WithInk(color));

    public RunComposer Ink(string hexColor) => Ink(Primitives.Ink.Hex(hexColor));

    public RunComposer Highlight(Ink color) => Refine(style => style.WithHighlight(color));

    public RunComposer Highlight(string hexColor) => Highlight(Primitives.Ink.Hex(hexColor));

    public RunComposer Weight(TypeWeight weight) => Refine(style => style.WithWeight(weight));

    public RunComposer Bold() => Refine(style => style.Bold());

    public RunComposer Italic(bool value = true) => Refine(style => style.Italic(value));

    public RunComposer Underline(bool value = true) => Refine(style => style.Underline(value));

    public RunComposer StrikeThrough(bool value = true) => Refine(style => style.StrikeThrough(value));

    public RunComposer Leading(float multiplier) => Refine(style => style.WithLeading(multiplier));

    public RunComposer Tracking(float spacing) => Refine(style => style.WithTracking(spacing));

    public RunComposer Subscript() => Refine(style => style.Subscript());

    public RunComposer Superscript() => Refine(style => style.Superscript());

    /// <summary>Applies an arbitrary style transformation.</summary>
    public RunComposer Style(Func<TypeStyle, TypeStyle> refinement) => Refine(refinement);
}
