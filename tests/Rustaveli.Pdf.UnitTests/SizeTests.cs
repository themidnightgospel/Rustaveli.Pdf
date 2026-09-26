namespace Rustaveli.Pdf.UnitTests;

public class SizeTests
{
    [Fact]
    public void ZeroIsEmptyInBothDimensions()
    {
        Assert.Equal(new Extent(0, 0), Extent.Zero);
    }

    [Fact]
    public void MaxIsTheLargestPagePdfAllows()
    {
        Assert.Equal(new Extent(14_400, 14_400), Extent.Max);
    }

    [Fact]
    public void WithWidthReplacesOnlyTheWidth()
    {
        Assert.Equal(new Extent(7, 2), new Extent(1, 2).WithWidth(7));
    }

    [Fact]
    public void WithHeightReplacesOnlyTheHeight()
    {
        Assert.Equal(new Extent(1, 7), new Extent(1, 2).WithHeight(7));
    }

    [Fact]
    public void ZeroIsNotNegative()
    {
        Assert.False(Extent.Zero.IsNegative);
    }

    [Fact]
    public void DriftWithinTheToleranceIsNotNegative()
    {
        Assert.False(new Extent(-Extent.Epsilon, -Extent.Epsilon).IsNegative);
    }

    [Theory]
    [InlineData(-0.002f, 0f)]
    [InlineData(0f, -0.002f)]
    public void EitherDimensionBeyondTheToleranceMakesItNegative(float width, float height)
    {
        Assert.True(new Extent(width, height).IsNegative);
    }

    [Fact]
    public void FitsInAnIdenticalSize()
    {
        Assert.True(new Extent(100, 50).FitsIn(new Extent(100, 50)));
    }

    [Fact]
    public void FitsWhenOvershootingByExactlyTheTolerance()
    {
        Assert.True(new Extent(100 + Extent.Epsilon, 50 + Extent.Epsilon).FitsIn(new Extent(100, 50)));
    }

    [Theory]
    [InlineData(100.01f, 50f)]
    [InlineData(100f, 50.01f)]
    public void DoesNotFitWhenEitherDimensionOvershootsBeyondTheTolerance(float width, float height)
    {
        Assert.False(new Extent(width, height).FitsIn(new Extent(100, 50)));
    }

    [Fact]
    public void FormatsBothDimensionsToThreeDecimals()
    {
        using CultureScope culture = CultureScope.Invariant();

        Assert.Equal("(Width: 1.500, Height: 2.346)", new Extent(1.5f, 2.3456f).ToString());
    }
}
