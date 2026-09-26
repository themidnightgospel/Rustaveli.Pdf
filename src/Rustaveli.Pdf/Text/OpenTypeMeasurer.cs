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

        return new TypeMetrics(font.ToPoints(lines.Ascent, size), font.ToPoints(lines.Descent, size), font.ToPoints(lines.LineGap, size));
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

    /// <summary>How far one glyph moves the pen: its advance, plus kerning and tracking from the glyph before it.</summary>
    private static float Step(ShapedGlyph glyph, float tracking, bool first) =>
        first ? glyph.Advance : glyph.Kerning + tracking + glyph.Advance;
}
