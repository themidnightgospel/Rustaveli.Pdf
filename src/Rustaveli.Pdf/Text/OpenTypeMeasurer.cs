using System.Globalization;
using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.Text;

/// <summary>
/// Measures type from the fonts themselves: advances, pair kerning and line metrics in design units, scaled.
/// </summary>
/// <remarks>
/// The widths are those the PDF surface positions glyphs by, because both walk the same <see cref="GlyphWalk"/>.
/// Tracking falls between characters — N characters have N - 1 gaps — so centred text stays centred: between what a
/// reader sees as characters, not between a letter and its marks, and not between the letters of a script written
/// joined, such as Arabic.
/// </remarks>
internal sealed class OpenTypeMeasurer(TypeShaper shaper) : ITypeMeasurer
{
    private readonly HashSet<int> _missing = [];

    /// <summary>
    /// The characters measured that no face has, which are set as missing-glyph boxes. Layout measures every word it
    /// sets, so after it these are all the document's; characters that need no glyph — controls, format characters
    /// and line and paragraph separators — are left out.
    /// </summary>
    public IReadOnlyCollection<int> MissingCodepoints => _missing;
    public TypeMetrics GetMetrics(TypeStyle style)
    {
        OpenTypeFont font = shaper.Resolve(style);
        LineMetrics lines = font.LineMetrics;
        float size = style.EffectivePointSize;

        // The tables give the top of each stroke; the lines are drawn along their centres.
        PostTable? post = font.Post;
        Os2Table? os2 = font.Os2;
        float underlineWeight = post is null ? 0f : font.ToPoints(post.UnderlineThickness, size);
        float underlineOffset = post is null ? 0f : -font.ToPoints(post.UnderlinePosition, size) + (underlineWeight / 2);
        float strikeWeight = os2 is null ? 0f : font.ToPoints(os2.StrikeoutSize, size);
        float strikeHeight = os2 is null ? 0f : font.ToPoints(os2.StrikeoutPosition, size) - (strikeWeight / 2);

        return new TypeMetrics(
            font.ToPoints(lines.Ascent, size),
            font.ToPoints(lines.Descent, size),
            font.ToPoints(lines.LineGap, size),
            Math.Max(0f, underlineOffset),
            Math.Max(0f, underlineWeight),
            Math.Max(0f, strikeHeight),
            Math.Max(0f, strikeWeight));
    }

    public TypeMetrics GetMetrics(string text, TypeStyle style)
    {
        TypeMetrics metrics = GetMetrics(style);
        OpenTypeFont primary = shaper.Resolve(style);

        // Text the style's own face has every character of is set in it alone, and is not walked: nearly all of it.
        if (!FallsBack(primary, text))
            return metrics;

        float size = style.EffectivePointSize;

        foreach (ShapedGlyph glyph in shaper.Measure(text.AsSpan(), style))
        {
            if (ReferenceEquals(glyph.Face, primary))
                continue;

            // The strokes stay the style's own, drawn once along the whole run.
            LineMetrics lines = glyph.Face.LineMetrics;
            metrics = metrics with
            {
                Ascent = Math.Max(metrics.Ascent, glyph.Face.ToPoints(lines.Ascent, size)),
                Descent = Math.Max(metrics.Descent, glyph.Face.ToPoints(lines.Descent, size)),
                LineGap = Math.Max(metrics.LineGap, glyph.Face.ToPoints(lines.LineGap, size)),
            };
        }

        return metrics;
    }

    /// <summary>Whether <paramref name="primary"/> lacks a character of <paramref name="text"/> that is drawn.</summary>
    private static bool FallsBack(OpenTypeFont primary, string text)
    {
        for (int index = 0; index < text.Length;)
        {
            int codepoint = GraphemeBoundaries.CodepointAt(text.AsSpan(), index, out int length);

            if (!primary.HasGlyph(codepoint) && !InvisibleCharacters.Contains(codepoint))
                return true;

            index += length;
        }

        return false;
    }

    public float MeasureWidth(string text, TypeStyle style)
    {
        if (string.IsNullOrEmpty(text))
            return 0f;

        float width = 0f;

        foreach (ShapedGlyph glyph in shaper.Measure(text.AsSpan(), style))
        {
            width += Step(glyph);

            if (glyph.Glyph == 0 && NeedsGlyph(glyph.Codepoint))
                _missing.Add(glyph.Codepoint);
        }

        return Math.Max(0f, width);
    }

    /// <summary>Whether a character is drawn at all, so that a face without it shows a missing-glyph box.</summary>
    private static bool NeedsGlyph(int codepoint) =>
        codepoint > 0xFFFF
        || CharUnicodeInfo.GetUnicodeCategory((char)codepoint) is not (
            UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator or UnicodeCategory.Surrogate);

    public int MeasureCharactersFitting(string text, TypeStyle style, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0)
            return 0;

        // The same sum MeasureWidth makes, stopped early, so a prefix this accepts measures within the width. It ends
        // where a character a reader sees as one begins, never between a letter and a mark set as a glyph of its own.
        float width = 0f;
        int cluster = 0;
        GraphemeBoundaries boundaries = default;

        foreach (ShapedGlyph glyph in shaper.Measure(text.AsSpan(), style))
        {
            if (glyph.Length > 0 && boundaries.Begins(text.AsSpan(), glyph.Start, glyph.Length))
                cluster = glyph.Start;

            width += Step(glyph);

            if (width > maxWidth)
                return cluster;
        }

        return text.Length;
    }

    /// <summary>
    /// How far one glyph moves the pen: kerning and tracking from the glyph before it, its advance, and any word
    /// spacing it carries. The first glyph has neither kerning nor tracking before it.
    /// </summary>
    private static float Step(ShapedGlyph glyph) => glyph.Kerning + glyph.Tracking + glyph.Advance + glyph.Extra;
}
