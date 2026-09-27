using Rustaveli.Pdf.Text;
using SkiaSharp;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Measurement against real fonts, asserted through relations that hold for any sensible face rather than
/// through widths that would pin one machine's copy of Arial, and checked against Skia reading the same file.
/// </summary>
public class OpenTypeMeasurerTests
{
    private static readonly OpenTypeMeasurer Measurer = new OpenTypeMeasurer(TypefaceLibrary.Shared.Shaper);
    private static readonly TypeStyle Style = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(20);

    /// <summary>A character outside the Basic Multilingual Plane: one character, two UTF-16 code units.</summary>
    private const string MathBoldA = "\U0001D400";

    /// <summary>
    /// Checks the fitting contract at every character boundary: a width midway between two consecutive prefixes
    /// holds the shorter one exactly, and a width beyond the whole text holds all of it.
    /// </summary>
    /// <param name="text">The text to fit.</param>
    /// <param name="style">The style it is measured in.</param>
    /// <param name="boundaries">Offsets, in UTF-16 code units, at which a character ends.</param>
    private static void AssertFitsWholeCharacters(string text, TypeStyle style, params int[] boundaries)
    {
        for (int index = 0; index + 1 < boundaries.Length; index++)
        {
            float fits = Measurer.MeasureWidth(text.Substring(0, boundaries[index]), style);
            float overflows = Measurer.MeasureWidth(text.Substring(0, boundaries[index + 1]), style);
            float midway = (fits + overflows) / 2;

            Assert.True(
                boundaries[index] == Measurer.MeasureCharactersFitting(text, style, midway),
                $"Within {midway:F2}pt, between the widths of the first {boundaries[index]} and {boundaries[index + 1]} code units.");
        }

        Assert.Equal(text.Length, Measurer.MeasureCharactersFitting(text, style, Measurer.MeasureWidth(text, style) + 1));
    }

    [Theory]
    [InlineData(7f)]
    [InlineData(11f)]
    [InlineData(13.5f)]
    public void WidthScalesExactlyWithPointSize(float size)
    {
        // Linear, unhinted metrics are the font's design units scaled, so width is proportional to size. Hinted
        // advances snap each size to the pixel grid differently — and differently per platform rasteriser, which
        // is how the same document came to space its words one way on Windows and another on Linux. Windows keeps
        // advances linear even when hinting, so this can only fail where the platform hints them: the Linux leg.
        const string Text = "Paragraph 1. The quick brown fox jumps over the lazy dog";
        float reference = Measurer.MeasureWidth(Text, Style.WithPointSize(100));

        float width = Measurer.MeasureWidth(Text, Style.WithPointSize(size));

        Assert.Equal(reference * size / 100f, width, 0.01f);
    }

    // ---- Metrics -----------------------------------------------------------------------------------------------

    [Fact]
    public void MetricsArePositiveDistancesProportionateToThePointSize()
    {
        TypeMetrics metrics = Measurer.GetMetrics(Style);

        // Skia reports ascent as a negative offset; a sign slip here inverts every line of text.
        Assert.InRange(metrics.Ascent, 0.6f * 20, 1.2f * 20);
        Assert.InRange(metrics.Descent, 0.1f * 20, 0.5f * 20);
        Assert.True(metrics.LineGap >= 0, $"Line gap was {metrics.LineGap}.");
        Assert.True(metrics.LineSpacing > 20, $"A 20pt line measured only {metrics.LineSpacing}pt.");
    }

    [Fact]
    public void MetricsAreTheFontsOwnAsPositiveDistances()
    {
        // An independent reading of the same file: Skia's, unhinted, at the same size. Within a thousandth of a point,
        // since the two scale font units in different orders and platforms round the last bit differently.
        using SKTypeface typeface = SKTypeface.FromFile(TestFonts.PathOf("NotoSans-Regular.ttf"));
        using SKFont font = new SKFont(typeface, Style.PointSize) { Hinting = SKFontHinting.None, LinearMetrics = true };
        SKFontMetrics native = font.Metrics;
        TypeMetrics metrics = Measurer.GetMetrics(Style);

        Assert.Equal(-native.Ascent, metrics.Ascent, 0.001f);
        Assert.Equal(native.Descent, metrics.Descent, 0.001f);

        // Leading is the one value a font may report as negative; a line gap can only add space.
        Assert.Equal(Math.Max(0f, native.Leading), metrics.LineGap, 0.001f);
    }

    [Fact]
    public void MetricsScaleWithThePointSize()
    {
        TypeMetrics regular = Measurer.GetMetrics(Style);
        TypeMetrics doubled = Measurer.GetMetrics(Style.WithPointSize(40));

        Assert.Equal(2 * regular.Ascent, doubled.Ascent, 0.02f * doubled.Ascent);
        Assert.Equal(2 * regular.Descent, doubled.Descent, 0.02f * doubled.Descent);
    }

    [Fact]
    public void SubscriptAndSuperscriptAreMeasuredAtTheirReducedSize()
    {
        TypeStyle reduced = Style.WithPointSize(Style.Superscript().EffectivePointSize);

        Assert.True(reduced.PointSize < Style.PointSize, "The premise needs a reduced size to compare against.");
        Assert.Equal(Measurer.GetMetrics(reduced).Ascent, Measurer.GetMetrics(Style.Superscript()).Ascent, 0.01f);
        Assert.Equal(Measurer.MeasureWidth("x2", reduced), Measurer.MeasureWidth("x2", Style.Subscript()), 0.01f);
    }

    // ---- Width -------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyTextHasNoWidth(string? text)
    {
        Assert.Equal(0f, Measurer.MeasureWidth(text!, Style));
        Assert.Equal(0f, Measurer.MeasureWidth(text!, Style.WithTracking(5)));
    }

    [Fact]
    public void WiderGlyphsAndHeavierWeightsMeasureWider()
    {
        Assert.True(Measurer.MeasureWidth("WWWW", Style) > Measurer.MeasureWidth("iiii", Style));
        Assert.True(Measurer.MeasureWidth("Heading", Style.Bold()) > Measurer.MeasureWidth("Heading", Style));
    }

    [Fact]
    public void WidthIsProportionalToThePointSize()
    {
        float regular = Measurer.MeasureWidth("Proportional", Style);

        Assert.Equal(2 * regular, Measurer.MeasureWidth("Proportional", Style.WithPointSize(40)), 0.01f * regular);
    }

    [Fact]
    public void TrackingAddsOneGapBetweenEachPairOfCharacters()
    {
        float plain = Measurer.MeasureWidth("ABCD", Style);

        Assert.Equal(plain + 3 * 5, Measurer.MeasureWidth("ABCD", Style.WithTracking(5)), 0.001f);
        Assert.Equal(plain - 3 * 1, Measurer.MeasureWidth("ABCD", Style.WithTracking(-1)), 0.001f);
    }

    [Fact]
    public void ASingleCharacterTakesNoTracking()
    {
        Assert.Equal(Measurer.MeasureWidth("A", Style), Measurer.MeasureWidth("A", Style.WithTracking(5)));
    }

    [Fact]
    public void ASurrogatePairIsOneCharacterForTracking()
    {
        string text = "A" + MathBoldA;

        Assert.Equal(Measurer.MeasureWidth(text, Style) + 5, Measurer.MeasureWidth(text, Style.WithTracking(5)), 0.001f);
    }

    [Fact]
    public void TighteningBeyondTheGlyphsThemselvesMeasuresAsNothing()
    {
        Assert.Equal(0f, Measurer.MeasureWidth("AB", Style.WithTracking(-1000)));
    }

    // ---- Fitting -----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0f)]
    [InlineData(-10f)]
    public void NothingFitsInNoSpace(float maxWidth)
    {
        Assert.Equal(0, Measurer.MeasureCharactersFitting("Hello", Style, maxWidth));
        Assert.Equal(0, Measurer.MeasureCharactersFitting("Hello", Style.WithTracking(5), maxWidth));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoCharactersOfEmptyTextFit(string? text)
    {
        Assert.Equal(0, Measurer.MeasureCharactersFitting(text!, Style, 100));
    }

    [Fact]
    public void FittingPlainTextAgreesWithItsMeasuredWidth()
    {
        AssertFitsWholeCharacters("Hello world", Style, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
    }

    [Fact]
    public void FittingLetterSpacedTextCountsTheGapsBetweenCharacters()
    {
        // Spacing wider than any glyph, so a gap counted before the first character would lose a whole one.
        AssertFitsWholeCharacters("Hello", Style.WithTracking(30), 0, 1, 2, 3, 4, 5);
    }

    [Fact]
    public void FittingTextThatSwitchesToAFallbackFontMeasuresEachRunInItsOwnFont()
    {
        AssertFitsWholeCharacters("Hello世界", Style, 0, 1, 2, 3, 4, 5, 6, 7);
    }

    [Fact]
    public void FittingNeverEndsInsideASurrogatePair()
    {
        string text = "A" + MathBoldA + "B";

        AssertFitsWholeCharacters(text, Style, 0, 1, 3, 4);
        AssertFitsWholeCharacters(text, Style.WithTracking(3), 0, 1, 3, 4);
    }
}
