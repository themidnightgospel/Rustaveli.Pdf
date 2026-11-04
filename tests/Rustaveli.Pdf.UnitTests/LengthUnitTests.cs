namespace Rustaveli.Pdf.UnitTests;

public class LengthUnitTests
{
    [Fact]
    public void ConvertsInchesUsingSeventyTwoPointsPerInch()
    {
        Approximately.Equal(72f, 1f.Inches());
    }

    [Fact]
    public void ConvertsMillimetresAgainstTheInch()
    {
        Approximately.Equal(72f, 25.4f.Millimetres());
    }

    [Fact]
    public void TreatsCentimetresAsTenMillimetres()
    {
        Approximately.Equal(10f.Millimetres(), 1f.Centimetres());
    }

    [Fact]
    public void LeavesPointsUnchanged()
    {
        Approximately.Equal(42f, 42f.Points());
    }

    [Theory]
    [InlineData(LengthUnit.Point, 2f)]
    [InlineData(LengthUnit.Millimetre, 5.669291f)]
    [InlineData(LengthUnit.Centimetre, 56.69291f)]
    [InlineData(LengthUnit.Metre, 5669.291f)]
    [InlineData(LengthUnit.Inch, 144f)]
    [InlineData(LengthUnit.Foot, 1728f)]
    [InlineData(LengthUnit.Mil, 0.144f)]
    [InlineData(LengthUnit.Pica, 24f)]
    public void ConvertsTwoOfEachUnitToPoints(LengthUnit unit, float expectedPoints)
    {
        Approximately.Equal(expectedPoints, 2f.ToPoints(unit));
    }

    [Fact]
    public void RejectsAnUndefinedUnit()
    {
        LengthUnit undefined = (LengthUnit)99;

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(() => 1f.ToPoints(undefined));

        Assert.Equal("unit", exception.ParamName);
        Assert.Equal(undefined, exception.ActualValue);
        Assert.StartsWith("Unsupported unit.", exception.Message);
    }

    [Fact]
    public void WholeNumberPointsAreUnchanged()
    {
        Approximately.Equal(7f, 7.Points());
    }

    [Fact]
    public void WholeNumberMillimetresConvertLikeTheirFractionalForm()
    {
        Approximately.Equal(720f, 254.Millimetres());
    }

    [Fact]
    public void WholeNumberCentimetresConvertLikeTheirFractionalForm()
    {
        Approximately.Equal(85.03937f, 3.Centimetres());
    }

    [Fact]
    public void WholeNumberInchesConvertLikeTheirFractionalForm()
    {
        Approximately.Equal(144f, 2.Inches());
    }

    [Fact]
    public void EveryUnitHasItsOwnConversionForWholeAndFractionalNumbers()
    {
        Approximately.Equal(2f.ToPoints(LengthUnit.Metre), 2f.Metres());
        Approximately.Equal(2f.ToPoints(LengthUnit.Metre), 2.Metres());
        Approximately.Equal(2f.ToPoints(LengthUnit.Foot), 2f.Feet());
        Approximately.Equal(2f.ToPoints(LengthUnit.Foot), 2.Feet());
        Approximately.Equal(2f.ToPoints(LengthUnit.Mil), 2f.Mils());
        Approximately.Equal(2f.ToPoints(LengthUnit.Mil), 2.Mils());
        Approximately.Equal(2f.ToPoints(LengthUnit.Pica), 2f.Picas());
        Approximately.Equal(2f.ToPoints(LengthUnit.Pica), 2.Picas());
    }

    [Fact]
    public void APicaIsTwelvePointsAndSixToTheInch()
    {
        Approximately.Equal(12f, 1.Picas());
        Approximately.Equal(1f.Inches(), 6.Picas());
    }

    // 529.2 points are 7.35 inches, 1470 pixels at 200 an inch. Divided by 72 first, as every caller has always worked
    // it out, float rounding takes the count just past 1470, to 1471; multiplied first, it stays at 1470.
    [Fact]
    public void PointsAreDividedIntoInchesBeforeTheyAreCountedInPixels() =>
        Assert.Equal(1471, Lengths.PixelCount(529.2f, 200));

    [Fact]
    public void AnythingAtAllTakesAPixel() =>
        Assert.Equal(1, Lengths.PixelCount(0.001f, 1));
}
