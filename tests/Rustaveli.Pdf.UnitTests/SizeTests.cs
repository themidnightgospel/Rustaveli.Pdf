namespace Rustaveli.Pdf.UnitTests;

public class SizeTests
{
    [Fact]
    public void ZeroIsEmptyInBothDimensions()
    {
        Assert.Equal(new Size(0, 0), Size.Zero);
    }

    [Fact]
    public void MaxIsTheLargestPagePdfAllows()
    {
        Assert.Equal(new Size(14_400, 14_400), Size.Max);
    }

    [Fact]
    public void WithWidthReplacesOnlyTheWidth()
    {
        Assert.Equal(new Size(7, 2), new Size(1, 2).WithWidth(7));
    }

    [Fact]
    public void WithHeightReplacesOnlyTheHeight()
    {
        Assert.Equal(new Size(1, 7), new Size(1, 2).WithHeight(7));
    }

    [Fact]
    public void ZeroIsNotNegative()
    {
        Assert.False(Size.Zero.IsNegative);
    }

    [Fact]
    public void DriftWithinTheToleranceIsNotNegative()
    {
        Assert.False(new Size(-Size.Epsilon, -Size.Epsilon).IsNegative);
    }

    [Theory]
    [InlineData(-0.002f, 0f)]
    [InlineData(0f, -0.002f)]
    public void EitherDimensionBeyondTheToleranceMakesItNegative(float width, float height)
    {
        Assert.True(new Size(width, height).IsNegative);
    }

    [Fact]
    public void FitsInAnIdenticalSize()
    {
        Assert.True(new Size(100, 50).FitsIn(new Size(100, 50)));
    }

    [Fact]
    public void FitsWhenOvershootingByExactlyTheTolerance()
    {
        Assert.True(new Size(100 + Size.Epsilon, 50 + Size.Epsilon).FitsIn(new Size(100, 50)));
    }

    [Theory]
    [InlineData(100.01f, 50f)]
    [InlineData(100f, 50.01f)]
    public void DoesNotFitWhenEitherDimensionOvershootsBeyondTheTolerance(float width, float height)
    {
        Assert.False(new Size(width, height).FitsIn(new Size(100, 50)));
    }

    [Fact]
    public void FormatsBothDimensionsToThreeDecimals()
    {
        using CultureScope culture = CultureScope.Invariant();

        Assert.Equal("(Width: 1.500, Height: 2.346)", new Size(1.5f, 2.3456f).ToString());
    }
}
