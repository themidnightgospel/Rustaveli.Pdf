namespace Rustaveli.Pdf.UnitTests;

public class ListMarkersTests
{
    private const string Bullet = "•";

    [Theory]
    [InlineData(ListNumbering.Bullet, 7, Bullet)]
    [InlineData(ListNumbering.Decimal, 12, "12.")]
    [InlineData(ListNumbering.LowerLetter, 1, "a.")]
    [InlineData(ListNumbering.UpperLetter, 26, "Z.")]
    [InlineData(ListNumbering.UpperLetter, 27, "AA.")]
    [InlineData(ListNumbering.UpperLetter, 52, "AZ.")]
    [InlineData(ListNumbering.UpperLetter, 53, "BA.")]
    [InlineData(ListNumbering.UpperLetter, 702, "ZZ.")]
    [InlineData(ListNumbering.LowerLetter, 703, "aaa.")]
    [InlineData(ListNumbering.UpperRoman, 1994, "MCMXCIV.")]
    [InlineData(ListNumbering.UpperRoman, 3999, "MMMCMXCIX.")]
    [InlineData(ListNumbering.LowerRoman, 49, "xlix.")]
    [InlineData(ListNumbering.LowerRoman, 444, "cdxliv.")]
    public void FormatsTheMarkerForAPosition(ListNumbering marker, int position, string expected)
    {
        Assert.Equal(expected, ListMarkers.Format(marker, position));
    }

    [Theory]
    [InlineData(ListNumbering.UpperRoman, 0, "0.")]
    [InlineData(ListNumbering.UpperRoman, 4000, "4000.")]
    [InlineData(ListNumbering.LowerRoman, 0, "0.")]
    [InlineData(ListNumbering.LowerRoman, 4000, "4000.")]
    public void FallsBackToDigitsOutsideTheRomanRange(ListNumbering marker, int position, string expected)
    {
        // Roman numerals have no zero, and nothing past a few thousand is legible.
        Assert.Equal(expected, ListMarkers.Format(marker, position));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void LettersStartAtAForPositionsBelowOne(int position)
    {
        Assert.Equal("A.", ListMarkers.Format(ListNumbering.UpperLetter, position));
    }

    [Fact]
    public void AnUnrecognisedMarkerFallsBackToABullet()
    {
        Assert.Equal(Bullet, ListMarkers.Format((ListNumbering)99, 3));
    }
}
