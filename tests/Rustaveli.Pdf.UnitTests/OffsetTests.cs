namespace Rustaveli.Pdf.UnitTests;

public class OffsetTests
{
    [Fact]
    public void ZeroIsTheOrigin()
    {
        Assert.Equal(new Offset(0, 0), Offset.Zero);
    }

    [Fact]
    public void ReverseNegatesBothCoordinates()
    {
        Assert.Equal(new Offset(-3, 4), new Offset(3, -4).Reverse());
    }

    [Fact]
    public void AdditionSumsEachCoordinate()
    {
        Assert.Equal(new Offset(11, 18), new Offset(1, 20) + new Offset(10, -2));
    }

    [Fact]
    public void FormatsBothCoordinatesToThreeDecimals()
    {
        using CultureScope culture = CultureScope.Invariant();

        Assert.Equal("(X: 1.500, Y: -2.346)", new Offset(1.5f, -2.3456f).ToString());
    }
}
