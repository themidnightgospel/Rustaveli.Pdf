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

    /// <summary>
    /// Sets the run in <paramref name="fontFamily"/>, falling back to <paramref name="fallbacks"/>, in order, for any
    /// character it lacks.
    /// </summary>
    public RunComposer Typeface(string fontFamily, params string[] fallbacks) =>
        Refine(style => style.WithTypeface(fontFamily, fallbacks));

    // Numbers are checked here, where the caller gives them, not when the run's style is worked out during layout.
    public RunComposer PointSize(float size)
    {
        TypeStyle.Size(size, nameof(size));
        return Refine(style => style.WithPointSize(size));
    }

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
    public RunComposer StrokeWeight(float weight)
    {
        TypeStyle.Size(weight, nameof(weight));
        return Refine(style => style.WithStrokeWeight(weight));
    }

    public RunComposer Leading(float multiplier)
    {
        TypeStyle.Size(multiplier, nameof(multiplier));
        return Refine(style => style.WithLeading(multiplier));
    }

    public RunComposer Tracking(float spacing)
    {
        TypeStyle.Spacing(spacing, nameof(spacing));
        return Refine(style => style.WithTracking(spacing));
    }

    /// <summary>Adds space to each space between words, in points; negative tightens.</summary>
    public RunComposer WordSpacing(float spacing)
    {
        TypeStyle.Spacing(spacing, nameof(spacing));
        return Refine(style => style.WithWordSpacing(spacing));
    }

    /// <summary>
    /// Reads the run left to right, set apart from the text around it: an English phrase in a right-to-left paragraph
    /// keeps its punctuation at its own end.
    /// </summary>
    public RunComposer LeftToRight() => Refine(style => style.WithDirection(ReadingDirection.LeftToRight));

    /// <summary>
    /// Reads the run right to left, set apart from the text around it: its words run from right to left even where
    /// they are written in a left-to-right script.
    /// </summary>
    public RunComposer RightToLeft() => Refine(style => style.WithDirection(ReadingDirection.RightToLeft));

    /// <summary>
    /// Sets the OpenType feature <paramref name="tag"/> — <c>"smcp"</c>, <c>"onum"</c>, <c>"ss01"</c> — to
    /// <paramref name="value"/>: 0 off, 1 on, higher to choose among alternates.
    /// </summary>
    public RunComposer Feature(string tag, int value = 1) => Refine(style => style.WithFeature(tag, value));

    /// <summary>Sets ligatures such as "fi" and "ffl", or not; they are on unless turned off.</summary>
    public RunComposer Ligatures(bool value = true) => Refine(style => style.Ligatures(value));

    /// <summary>Sets lowercase letters as small capitals, where the face has them.</summary>
    public RunComposer SmallCapitals(bool value = true) => Refine(style => style.SmallCapitals(value));

    /// <summary>Sets old-style figures, which rise and descend like lowercase letters, where the face has them.</summary>
    public RunComposer OldstyleFigures(bool value = true) => Refine(style => style.OldstyleFigures(value));

    /// <summary>Sets figures all one width, so columns of numbers align, where the face has them.</summary>
    public RunComposer TabularFigures(bool value = true) => Refine(style => style.TabularFigures(value));

    /// <summary>Lets lines break between any two characters of the run, not only between words.</summary>
    public RunComposer BreakAnywhere(bool value = true) => Refine(style => style.BreakAnywhere(value));

    public RunComposer Subscript() => Refine(style => style.Subscript());

    public RunComposer Superscript() => Refine(style => style.Superscript());

    /// <summary>Applies an arbitrary style transformation.</summary>
    public RunComposer Style(Func<TypeStyle, TypeStyle> refinement) => Refine(refinement);

    /// <summary>Sets the run in the document's type style named <paramref name="name"/>.</summary>
    public RunComposer Style(string name) => Refine(StyleSheet.InForce.Type(name));
}
