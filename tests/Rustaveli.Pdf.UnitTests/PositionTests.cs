namespace Rustaveli.Pdf.UnitTests;

public class PositionTests
{
    [Fact]
    public void ZeroIsTheOrigin()
    {
        Assert.Equal(new Position(0, 0), Position.Zero);
    }

    [Fact]
    public void ReverseNegatesBothCoordinates()
    {
        Assert.Equal(new Position(-3, 4), new Position(3, -4).Reverse());
    }

    [Fact]
    public void AdditionSumsEachCoordinate()
    {
        Assert.Equal(new Position(11, 18), new Position(1, 20) + new Position(10, -2));
    }

    [Fact]
    public void FormatsBothCoordinatesToThreeDecimals()
    {
        using CultureScope culture = CultureScope.Invariant();

        Assert.Equal("(X: 1.500, Y: -2.346)", new Position(1.5f, -2.3456f).ToString());
    }
}
