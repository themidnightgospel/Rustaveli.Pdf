namespace Rustaveli.Pdf.UnitTests;

public class CornersTests
{
    [Fact]
    public void AllRoundsEveryCornerAlike()
    {
        Assert.Equal(new Corners(3, 3, 3, 3), Corners.All(3));
        Assert.Equal(new Corners(0, 0, 0, 0), Corners.Zero);
    }

    [Fact]
    public void EachCornerCanBeChangedAlone()
    {
        Corners corners = Corners.Zero.WithTopLeft(1).WithTopRight(2).WithBottomRight(3).WithBottomLeft(4);

        Assert.Equal(new Corners(1, 2, 3, 4), corners);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, false)]
    [InlineData(1, 0, 0, 0, true)]
    [InlineData(0, 1, 0, 0, true)]
    [InlineData(0, 0, 1, 0, true)]
    [InlineData(0, 0, 0, 1, true)]
    public void AnyRadiusRoundsTheShape(float topLeft, float topRight, float bottomRight, float bottomLeft, bool rounded) =>
        Assert.Equal(rounded, new Corners(topLeft, topRight, bottomRight, bottomLeft).IsRounded);

    [Fact]
    public void RadiiThatFitAreKept() =>
        Assert.Equal(new Corners(1, 2, 3, 4), new Corners(1, 2, 3, 4).FittedTo(new Extent(100, 100)));

    [Fact]
    public void EqualRadiiEndAtHalfTheShorterSide() =>
        Approximately.Equal(10f, Corners.All(50).FittedTo(new Extent(60, 20)).TopLeft);

    [Fact]
    public void OverlappingRadiiAreScaledDownTogether()
    {
        // The top side is 40 long but its corners ask for 60, so every radius is scaled by two thirds.
        Corners fitted = new Corners(30, 30, 3, 6).FittedTo(new Extent(40, 100));

        Approximately.Equal(20f, fitted.TopLeft);
        Approximately.Equal(20f, fitted.TopRight);
        Approximately.Equal(2f, fitted.BottomRight);
        Approximately.Equal(4f, fitted.BottomLeft);
    }

    [Theory]
    [InlineData(40, 100, 30, 30, 0, 0)]
    [InlineData(100, 40, 30, 0, 0, 30)]
    [InlineData(100, 40, 0, 30, 30, 0)]
    [InlineData(40, 100, 0, 0, 30, 30)]
    public void EverySideIsChecked(float width, float height, float topLeft, float topRight, float bottomRight, float bottomLeft)
    {
        Corners fitted = new Corners(topLeft, topRight, bottomRight, bottomLeft).FittedTo(new Extent(width, height));

        // Whichever side the two 30s share, 40 of room scales them to 20.
        Assert.Equal(20f, Math.Max(Math.Max(fitted.TopLeft, fitted.TopRight), Math.Max(fitted.BottomRight, fitted.BottomLeft)), 3);
    }

    [Fact]
    public void ANegativeRadiusIsSquare() =>
        Assert.Equal(new Corners(0, 2, 0, 0), new Corners(-5, 2, -1, 0).FittedTo(new Extent(100, 100)));

    [Fact]
    public void AShapeWithNoSizeHasNoRounding() =>
        Assert.Equal(Corners.Zero, Corners.All(5).FittedTo(new Extent(0, 0)));
}
