using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.Text;

/// <summary>
/// Measures type from the fonts themselves: advances, pair kerning and line metrics in design units, scaled.
/// </summary>
/// <remarks>
/// The widths are those the PDF surface positions glyphs by, because both walk the same <see cref="GlyphWalk"/>.
/// Tracking falls between characters — N characters have N - 1 gaps — so centred text stays centred.
/// </remarks>
internal sealed class OpenTypeMeasurer(TypeShaper shaper) : ITypeMeasurer
{
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

    public float MeasureWidth(string text, TypeStyle style)
    {
        if (string.IsNullOrEmpty(text))
            return 0f;

        float width = 0f;
        bool first = true;

        foreach (ShapedGlyph glyph in shaper.Walk(text.AsSpan(), style))
        {
            width += Step(glyph, style.Tracking, first);
            first = false;
        }

        return Math.Max(0f, width);
    }

    public int MeasureCharactersFitting(string text, TypeStyle style, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0)
            return 0;

        // The same sum MeasureWidth makes, stopped early, so a prefix this accepts measures within the width.
        float width = 0f;
        bool first = true;

        foreach (ShapedGlyph glyph in shaper.Walk(text.AsSpan(), style))
        {
            width += Step(glyph, style.Tracking, first);
            first = false;

            if (width > maxWidth)
                return glyph.Start;
        }

        return text.Length;
    }

    /// <summary>
    /// How far one glyph moves the pen: kerning and tracking from the glyph before it, its advance, and any word
    /// spacing it carries.
    /// </summary>
    private static float Step(ShapedGlyph glyph, float tracking, bool first) =>
        (first ? 0f : glyph.Kerning + tracking) + glyph.Advance + glyph.Extra;
}
