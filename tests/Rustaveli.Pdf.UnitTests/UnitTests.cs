namespace Rustaveli.Pdf.UnitTests;

public class UnitTests
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
    [InlineData(Unit.Point, 2f)]
    [InlineData(Unit.Millimetre, 5.669291f)]
    [InlineData(Unit.Centimetre, 56.69291f)]
    [InlineData(Unit.Metre, 5669.291f)]
    [InlineData(Unit.Inch, 144f)]
    [InlineData(Unit.Feet, 1728f)]
    public void ConvertsTwoOfEachUnitToPoints(Unit unit, float expectedPoints)
    {
        Approximately.Equal(expectedPoints, 2f.ToPoints(unit));
    }

    [Fact]
    public void RejectsAnUndefinedUnit()
    {
        Unit undefined = (Unit)99;

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
}
