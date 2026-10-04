using System.Globalization;
using System.Runtime.CompilerServices;
using Rustaveli.Pdf.Fonts;
#if NET
using WidthTable = System.Collections.Generic.Dictionary<string, float>;
#else
using WidthTable = System.Collections.Generic.Dictionary<Rustaveli.Pdf.Text.WidthKey, float>;
#endif

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
    /// The width of each piece of text measured, by style and then by text. Layout measures a document word by word,
    /// and most words recur; a width depends on nothing but the text, the style and this measurer's faces, so each is
    /// shaped once. Styles are compared by identity: they are made once and shared by every run set in them, and
    /// comparing two by their values on every word measured would cost what the width saves.
    /// </summary>
    private readonly Dictionary<TypeStyle, WidthTable> _widths = new Dictionary<TypeStyle, WidthTable>(new ByIdentity());

    /// <summary>The style measured last and its widths: words come a line at a time, most of them in one style.</summary>
    private (TypeStyle? Style, WidthTable? Widths) _last;

#if !NET
    /// <summary>The characters of the text being looked up, so that a word measured before costs no string.</summary>
    private char[] _probe = new char[32];
#endif

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

    public TypeMetrics GetMetrics(ReadOnlySpan<char> text, TypeStyle style)
    {
        TypeMetrics metrics = GetMetrics(style);
        OpenTypeFont primary = shaper.Resolve(style);

        // Text the style's own face has every character of is set in it alone, and is not walked: nearly all of it.
        if (!FallsBack(primary, text))
            return metrics;

        float size = style.EffectivePointSize;

        foreach (ShapedGlyph glyph in shaper.Measure(text, style))
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
    private static bool FallsBack(OpenTypeFont primary, ReadOnlySpan<char> text)
    {
        for (int index = 0; index < text.Length;)
        {
            int codepoint = GraphemeBoundaries.CodepointAt(text, index, out int length);

            if (!primary.HasGlyph(codepoint) && !InvisibleCharacters.Contains(codepoint))
                return true;

            index += length;
        }

        return false;
    }

    public float MeasureWidth(ReadOnlySpan<char> text, TypeStyle style)
    {
        if (text.IsEmpty)
            return 0f;

        WidthTable widths = WidthsIn(style);

        // Looked up by the characters themselves, so a word measured before costs no string.
#if NET
        Dictionary<string, float>.AlternateLookup<ReadOnlySpan<char>> known = widths.GetAlternateLookup<ReadOnlySpan<char>>();

        if (known.TryGetValue(text, out float width))
            return width;
#else
        if (_probe.Length < text.Length)
            _probe = new char[Math.Max(text.Length, _probe.Length * 2)];

        text.CopyTo(_probe);

        if (widths.TryGetValue(new WidthKey(_probe, text.Length), out float width))
            return width;
#endif

        width = 0f;

        foreach (ShapedGlyph glyph in shaper.Measure(text, style))
        {
            width += Step(glyph);

            if (glyph.Glyph == 0 && NeedsGlyph(glyph.Codepoint))
                _missing.Add(glyph.Codepoint);
        }

        width = Math.Max(0f, width);

#if NET
        known[text] = width;
#else
        widths[new WidthKey(text.ToString())] = width;
#endif

        return width;
    }

    private WidthTable WidthsIn(TypeStyle style)
    {
        if (ReferenceEquals(_last.Style, style))
            return _last.Widths!;

#if NET
        if (!_widths.TryGetValue(style, out WidthTable? widths))
            _widths[style] = widths = new WidthTable(StringComparer.Ordinal);
#else
        if (!_widths.TryGetValue(style, out WidthTable? widths))
            _widths[style] = widths = new WidthTable(WidthKey.Comparer);
#endif

        _last = (style, widths);
        return widths;
    }

    /// <summary>A style compared by identity.</summary>
    private sealed class ByIdentity : IEqualityComparer<TypeStyle>
    {
        public bool Equals(TypeStyle? x, TypeStyle? y) => ReferenceEquals(x, y);

        public int GetHashCode(TypeStyle style) => RuntimeHelpers.GetHashCode(style);
    }

    /// <summary>Whether a character is drawn at all, so that a face without it shows a missing-glyph box.</summary>
    private static bool NeedsGlyph(int codepoint) =>
        codepoint > 0xFFFF
        || CharUnicodeInfo.GetUnicodeCategory((char)codepoint) is not (
            UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator or UnicodeCategory.Surrogate);

    public int MeasureCharactersFitting(ReadOnlySpan<char> text, TypeStyle style, float maxWidth)
    {
        if (text.IsEmpty || maxWidth <= 0)
            return 0;

        // The same sum MeasureWidth makes, stopped early, so a prefix this accepts measures within the width. It ends
        // where a character a reader sees as one begins, never between a letter and a mark set as a glyph of its own.
        float width = 0f;
        int cluster = 0;
        GraphemeBoundaries boundaries = default;

        foreach (ShapedGlyph glyph in shaper.Measure(text, style))
        {
            if (glyph.Length > 0 && boundaries.Begins(text, glyph.Start, glyph.Length))
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
