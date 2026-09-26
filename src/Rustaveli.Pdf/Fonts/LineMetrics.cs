namespace Rustaveli.Pdf.Fonts;

/// <summary>
/// A font's vertical metrics for setting lines of text, in font units, as positive distances.
/// </summary>
/// <remarks>
/// <para>
/// Fonts carry three sets of vertical metrics that often disagree, and Skia — which the layout engine was built
/// against — reported different ones per platform. The choice here is the one Skia makes on Linux through FreeType,
/// so existing layouts keep their line heights wherever the platforms agreed:
/// </para>
/// <list type="number">
/// <item>the typographic metrics from <c>OS/2</c> when the font sets USE_TYPO_METRICS (fsSelection bit 7) — the
/// designer asking for exactly that, which DirectWrite honours as well;</item>
/// <item>otherwise the <c>hhea</c> metrics, which FreeType and Core Text use;</item>
/// <item>otherwise — when <c>hhea</c> gives zero for both — the typographic metrics, then the Windows ones, as
/// FreeType falls back.</item>
/// </list>
/// <para>
/// DirectWrite alone uses usWinAscent and usWinDescent in the second case, so a font that does not set
/// USE_TYPO_METRICS and whose Windows metrics differ from <c>hhea</c> now gets the line height it had on Linux and
/// macOS, where before it had another on Windows. The committed Noto fonts set the bit, and their typographic and
/// <c>hhea</c> metrics agree, so they lay out exactly as before on every platform.
/// </para>
/// </remarks>
/// <param name="Ascent">Distance from the baseline up to the top of the line.</param>
/// <param name="Descent">Distance from the baseline down to the bottom of the line.</param>
/// <param name="LineGap">Extra space the font recommends between lines; never negative.</param>
/// <param name="Source">Which of the font's metrics these are.</param>
internal readonly record struct LineMetrics(int Ascent, int Descent, int LineGap, LineMetricsSource Source)
{
    /// <summary>The distance from one baseline to the next.</summary>
    public int LineHeight => Ascent + Descent + LineGap;

    public static LineMetrics Choose(HorizontalHeaderTable hhea, Os2Table? os2)
    {
        bool hasOs2Metrics = os2 is { HasLineMetrics: true };

        if (hasOs2Metrics && os2!.UseTypoMetrics)
            return Typographic(os2);

        if (hhea.Ascender != 0 || hhea.Descender != 0 || !hasOs2Metrics)
            return From(hhea.Ascender, hhea.Descender, hhea.LineGap, LineMetricsSource.HorizontalHeader);

        if (os2!.TypoAscender != 0 || os2.TypoDescender != 0)
            return Typographic(os2);

        return new LineMetrics(os2.WinAscent, os2.WinDescent, 0, LineMetricsSource.Windows);
    }

    private static LineMetrics Typographic(Os2Table os2) =>
        From(os2.TypoAscender, os2.TypoDescender, os2.TypoLineGap, LineMetricsSource.Typographic);

    /// <summary>
    /// Fonts store the descender as a negative offset. Its magnitude is taken, because a few fonts get the sign
    /// wrong, and a positive descender would otherwise put the bottom of every line above its baseline.
    /// </summary>
    private static LineMetrics From(int ascender, int descender, int lineGap, LineMetricsSource source) =>
        new LineMetrics(ascender, Math.Abs(descender), Math.Max(0, lineGap), source);
}
