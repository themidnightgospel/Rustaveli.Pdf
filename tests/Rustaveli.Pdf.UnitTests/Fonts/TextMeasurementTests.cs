using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>Measuring text: widths with and without kerning, tracking, and how much text fits a width.</summary>
public class TextMeasurementTests
{
    private static readonly OpenTypeFont Font = TestFonts.Regular;

    [Fact]
    public void AddsAdvancesAndPairKerning()
    {
        // A 639 + V 600 + A 639, with -40 kerning at each of the two pairs.
        Assert.Equal(1878, Font.MeasureWidthInUnits("AVA", kerning: false));
        Assert.Equal(1798, Font.MeasureWidthInUnits("AVA"));
        Assert.Equal(1798 * 12f / 1000f, Font.MeasureWidth("AVA", 12f), 4);
        Assert.Equal(1878 * 12f / 1000f, Font.MeasureWidth("AVA", 12f, kerning: false), 4);
    }

    [Fact]
    public void AddsTrackingBetweenCharactersOnly()
    {
        float plain = Font.MeasureWidth("To", 10f);

        Assert.Equal(plain + 2f, Font.MeasureWidth("To", 10f, tracking: 2f), 4);
        Assert.Equal(Font.MeasureWidth("T", 10f), Font.MeasureWidth("T", 10f, tracking: 5f), 4);
    }

    [Fact]
    public void MeasuresNothingAsZero()
    {
        Assert.Equal(0f, Font.MeasureWidth(string.Empty, 12f, tracking: 3f));
        Assert.Equal(0, Font.MeasureWidthInUnits(string.Empty));
        Assert.Equal(0, Font.CountFitting(string.Empty, 12f, 100f));
    }

    [Fact]
    public void MeasuresASurrogatePairAsOneCharacter()
    {
        // U+1D400 (mathematical bold A) is not in Noto Sans, so it measures as one .notdef, and tracking counts it
        // as one character.
        const string Text = "A\U0001D400";
        int notdef = Font.GetAdvance(0);

        Assert.Equal(639 + notdef, Font.MeasureWidthInUnits(Text, kerning: false));
        Assert.Equal(((639 + notdef) / 100f) + 1f, Font.MeasureWidth(Text, 10f, tracking: 1f, kerning: false), 4);
    }

    [Fact]
    public void MeasuresALoneSurrogateAsAMissingGlyph()
    {
        int notdef = Font.GetAdvance(0);

        Assert.Equal(notdef, Font.MeasureWidthInUnits("\uD835", kerning: false));
        Assert.Equal(2 * notdef, Font.MeasureWidthInUnits("\uDC00\uD835", kerning: false));
    }

    [Fact]
    public void CountsTheCharactersThatFit()
    {
        const string Text = "AVAVA";
        float two = Font.MeasureWidth("AV", 20f);
        float three = Font.MeasureWidth("AVA", 20f);

        Assert.Equal(2, Font.CountFitting(Text, 20f, two));
        Assert.Equal(2, Font.CountFitting(Text, 20f, (two + three) / 2));
        Assert.Equal(3, Font.CountFitting(Text, 20f, three));
        Assert.Equal(Text.Length, Font.CountFitting(Text, 20f, 1000f));
        Assert.Equal(0, Font.CountFitting(Text, 20f, 1f));
        Assert.Equal(0, Font.CountFitting(Text, 20f, 0f));
        Assert.Equal(0, Font.CountFitting(Text, 20f, -5f));
    }

    [Fact]
    public void CountsWithKerningAndTrackingAsMeasuringDoes()
    {
        const string Text = "To Yo AVA P.";

        for (int length = 1; length <= Text.Length; length++)
        {
            string prefix = Text.Substring(0, length);

            foreach ((float tracking, bool kerning) in new[] { (0f, true), (0f, false), (1.5f, true), (-0.5f, true) })
            {
                float width = Font.MeasureWidth(prefix, 13.5f, tracking, kerning);

                Assert.True(
                    Font.CountFitting(Text, 13.5f, width, tracking, kerning) >= length,
                    $"'{prefix}' measures {width} with tracking {tracking}, kerning {kerning}, but did not fit it.");
            }
        }
    }

    [Fact]
    public void NeverSplitsASurrogatePair()
    {
        const string Text = "A\U0001D400B";
        float withPair = Font.MeasureWidth(Text.Substring(0, 3), 10f);

        Assert.Equal(1, Font.CountFitting(Text, 10f, withPair - 0.01f));
        Assert.Equal(3, Font.CountFitting(Text, 10f, withPair));
    }

    [Fact]
    public void MeasuresGeorgianWithItsOwnKerning()
    {
        OpenTypeFont georgian = TestFonts.Georgian;

        // ვ (U+10D5) then ა (U+10D0): advances 581 and 549, kerned -20.
        Assert.Equal(581 + 549 - 20, georgian.MeasureWidthInUnits("\u10D5\u10D0"));
    }
}
