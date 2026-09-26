using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf;

/// <summary>
/// Refines the style of a single span.
/// </summary>
/// <remarks>
/// Each call composes another transformation on top of the inherited style rather than replacing it, so
/// <c>.Bold().FontSize(18)</c> applies both and still inherits the typeface from its surroundings.
/// </remarks>
public sealed class RunComposer
{
    private readonly TextRun _run;

    internal RunComposer(TextRun run) => _run = run;

    private RunComposer Refine(Func<TypeStyle, TypeStyle> refinement)
    {
        Func<TypeStyle, TypeStyle>? previous = _run.Refinement;

        _run.Refinement = previous is null
            ? refinement
            : style => refinement(previous(style));

        return this;
    }

    public RunComposer Typeface(string fontFamily) => Refine(style => style.WithTypeface(fontFamily));

    public RunComposer PointSize(float size) => Refine(style => style.WithPointSize(size));

    public RunComposer Ink(Ink ink) => Refine(style => style.WithInk(ink));

    public RunComposer Ink(string hex) => Ink(Rustaveli.Pdf.Ink.Hex(hex));

    public RunComposer Highlight(Ink ink) => Refine(style => style.WithHighlight(ink));

    public RunComposer Highlight(string hex) => Highlight(Rustaveli.Pdf.Ink.Hex(hex));

    public RunComposer Weight(TypeWeight weight) => Refine(style => style.WithWeight(weight));

    public RunComposer Bold() => Refine(style => style.Bold());

    public RunComposer Italic(bool value = true) => Refine(style => style.Italic(value));

    public RunComposer Underline(bool value = true) => Refine(style => style.Underline(value));

    public RunComposer StrikeThrough(bool value = true) => Refine(style => style.StrikeThrough(value));

    /// <summary>Draws a line above the run.</summary>
    public RunComposer Overline(bool value = true) => Refine(style => style.Overline(value));

    /// <summary>How the run's underline, strike-through and overline are drawn: solid, double, dotted, dashed or wavy.</summary>
    public RunComposer StrokeStyle(StrokeStyle style) => Refine(current => current.WithStrokeStyle(style));

    /// <summary>The ink of the run's underline, strike-through and overline, in place of the text's own.</summary>
    public RunComposer StrokeInk(Ink ink) => Refine(style => style.WithStrokeInk(ink));

    public RunComposer StrokeInk(string hex) => StrokeInk(Rustaveli.Pdf.Ink.Hex(hex));

    /// <summary>The weight of the run's underline, strike-through and overline, in points, in place of the font's own.</summary>
    public RunComposer StrokeWeight(float weight) => Refine(style => style.WithStrokeWeight(weight));

    public RunComposer Leading(float multiplier) => Refine(style => style.WithLeading(multiplier));

    public RunComposer Tracking(float spacing) => Refine(style => style.WithTracking(spacing));

    /// <summary>Adds space to each space between words, in points; negative tightens.</summary>
    public RunComposer WordSpacing(float spacing) => Refine(style => style.WithWordSpacing(spacing));

    /// <summary>Lets lines break between any two characters of the run, not only between words.</summary>
    public RunComposer BreakAnywhere(bool value = true) => Refine(style => style.BreakAnywhere(value));

    public RunComposer Subscript() => Refine(style => style.Subscript());

    public RunComposer Superscript() => Refine(style => style.Superscript());

    /// <summary>Applies an arbitrary style transformation.</summary>
    public RunComposer Style(Func<TypeStyle, TypeStyle> refinement) => Refine(refinement);
}
