using Rustaveli.Pdf.Text;
using SkiaSharp;

namespace Rustaveli.Pdf.Skia;

/// <summary>
/// Measures text using Skia's font metrics.
/// </summary>
public sealed class SkiaTextMeasurer(SkiaFontProvider fonts) : ITextMeasurer
{
    public FontMetrics GetMetrics(TextStyle style)
    {
        SKFont font = fonts.GetFont(style);
        SKFontMetrics metrics = font.Metrics;

        // Skia reports ascent as a negative offset from the baseline; the layout engine expects both distances
        // to be positive magnitudes.
        return new FontMetrics(
            Ascent: -metrics.Ascent,
            Descent: metrics.Descent,
            LineGap: Math.Max(0, metrics.Leading));
    }

    public float MeasureWidth(string text, TextStyle style)
    {
        if (string.IsNullOrEmpty(text))
            return 0f;

        // Summed per font run, so a string that needs a fallback for part of itself is measured with the same
        // typefaces it will be drawn with.
        float width = 0f;

        foreach (FontRun run in fonts.Split(text, style))
            width += run.Font.MeasureText(run.Text);

        // Tracking sits between characters, so N characters have N-1 gaps. Counting a trailing gap would make
        // every run one letter-space too wide, pushing centred text off-centre and overshooting underlines.
        if (style.LetterSpacing != 0)
            width += style.LetterSpacing * Math.Max(0, CountTextElements(text) - 1);

        return Math.Max(0, width);
    }

    /// <summary>
    /// Counts user-perceived characters, so a surrogate pair or a combining sequence is one unit rather than two.
    /// </summary>
    private static int CountTextElements(string text)
    {
        int count = 0;

        for (int index = 0; index < text.Length; index += char.IsSurrogatePair(text, index) ? 2 : 1)
            count++;

        return count;
    }

    public int MeasureCharactersFitting(string text, TextStyle style, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0)
            return 0;

        IReadOnlyList<FontRun> runs = fonts.Split(text, style);

        // A single run with no tracking is the common case, and Skia can answer it directly.
        if (style.LetterSpacing == 0 && runs.Count == 1)
            return (int)runs[0].Font.BreakText(text, maxWidth);

        // Otherwise walk whole characters, switching font at each run boundary. Never split a surrogate pair:
        // measuring the halves separately would report two unmapped glyphs and could return an index that cuts
        // a character down the middle.
        float width = 0f;
        int consumed = 0;

        foreach (FontRun run in runs)
        {
            int position = 0;

            while (position < run.Text.Length)
            {
                int length = char.IsSurrogatePair(run.Text, position) ? 2 : 1;

                if (consumed > 0)
                    width += style.LetterSpacing;

                width += run.Font.MeasureText(run.Text.AsSpan(position, length));

                if (width > maxWidth)
                    return consumed;

                position += length;
                consumed += length;
            }
        }

        return text.Length;
    }
}
