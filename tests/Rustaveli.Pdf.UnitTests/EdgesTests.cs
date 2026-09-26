namespace Rustaveli.Pdf.UnitTests;

public class EdgesTests
{
    [Fact]
    public void SumsOpposingSides()
    {
        Edges edges = new Edges(1, 2, 4, 8);

        Approximately.Equal(5f, edges.Horizontal);
        Approximately.Equal(10f, edges.Vertical);
    }

    [Fact]
    public void ZeroHasNoWidthOnAnySide()
    {
        Assert.Equal(new Edges(0, 0, 0, 0), Edges.Zero);
    }

    [Fact]
    public void AllAppliesTheSameValueToEverySide()
    {
        Assert.Equal(new Edges(3, 3, 3, 3), Edges.All(3));
    }

    [Fact]
    public void SymmetricPairsTheHorizontalAndVerticalSides()
    {
        Assert.Equal(new Edges(Left: 3, Top: 5, Right: 3, Bottom: 5), Edges.Symmetric(horizontal: 3, vertical: 5));
    }

    [Fact]
    public void WithLeftReplacesOnlyTheLeftSide()
    {
        Assert.Equal(new Edges(9, 2, 3, 4), new Edges(1, 2, 3, 4).WithLeft(9));
    }

    [Fact]
    public void WithTopReplacesOnlyTheTopSide()
    {
        Assert.Equal(new Edges(1, 9, 3, 4), new Edges(1, 2, 3, 4).WithTop(9));
    }

    [Fact]
    public void WithRightReplacesOnlyTheRightSide()
    {
        Assert.Equal(new Edges(1, 2, 9, 4), new Edges(1, 2, 3, 4).WithRight(9));
    }

    [Fact]
    public void WithBottomReplacesOnlyTheBottomSide()
    {
        Assert.Equal(new Edges(1, 2, 3, 9), new Edges(1, 2, 3, 4).WithBottom(9));
    }
}
