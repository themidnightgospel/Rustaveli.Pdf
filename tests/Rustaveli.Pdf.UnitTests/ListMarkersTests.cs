namespace Rustaveli.Pdf.UnitTests;

public class ListMarkersTests
{
    private const string Bullet = "•";

    [Theory]
    [InlineData(ListMarker.Bullet, 7, Bullet)]
    [InlineData(ListMarker.Decimal, 12, "12.")]
    [InlineData(ListMarker.LowerLetter, 1, "a.")]
    [InlineData(ListMarker.UpperLetter, 26, "Z.")]
    [InlineData(ListMarker.UpperLetter, 27, "AA.")]
    [InlineData(ListMarker.UpperLetter, 52, "AZ.")]
    [InlineData(ListMarker.UpperLetter, 53, "BA.")]
    [InlineData(ListMarker.UpperLetter, 702, "ZZ.")]
    [InlineData(ListMarker.LowerLetter, 703, "aaa.")]
    [InlineData(ListMarker.UpperRoman, 1994, "MCMXCIV.")]
    [InlineData(ListMarker.UpperRoman, 3999, "MMMCMXCIX.")]
    [InlineData(ListMarker.LowerRoman, 49, "xlix.")]
    [InlineData(ListMarker.LowerRoman, 444, "cdxliv.")]
    public void FormatsTheMarkerForAPosition(ListMarker marker, int position, string expected)
    {
        Assert.Equal(expected, ListMarkers.Format(marker, position));
    }

    [Theory]
    [InlineData(ListMarker.UpperRoman, 0, "0.")]
    [InlineData(ListMarker.UpperRoman, 4000, "4000.")]
    [InlineData(ListMarker.LowerRoman, 0, "0.")]
    [InlineData(ListMarker.LowerRoman, 4000, "4000.")]
    public void FallsBackToDigitsOutsideTheRomanRange(ListMarker marker, int position, string expected)
    {
        // Roman numerals have no zero, and nothing past a few thousand is legible.
        Assert.Equal(expected, ListMarkers.Format(marker, position));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void LettersStartAtAForPositionsBelowOne(int position)
    {
        Assert.Equal("A.", ListMarkers.Format(ListMarker.UpperLetter, position));
    }

    [Fact]
    public void AnUnrecognisedMarkerFallsBackToABullet()
    {
        Assert.Equal(Bullet, ListMarkers.Format((ListMarker)99, 3));
    }
}
