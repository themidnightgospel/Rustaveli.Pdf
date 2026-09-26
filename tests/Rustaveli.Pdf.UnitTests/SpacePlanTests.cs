namespace Rustaveli.Pdf.UnitTests;

public class SpacePlanTests
{
    [Fact]
    public void WrapCarriesItsReasonAndOccupiesNoSpace()
    {
        Fit plan = Fit.Wrap("too narrow");

        Assert.True(plan.IsWrap);
        Assert.Equal("too narrow", plan.WrapReason);
        Assert.Equal(Extent.Zero, plan.Size);
    }

    [Fact]
    public void OnlyRenderingOutcomesCountAsHavingDrawn()
    {
        Assert.True(Fit.FullRender(new Extent(1, 1)).DrewSomething);
        Assert.True(Fit.PartialRender(new Extent(1, 1)).DrewSomething);
        Assert.False(Fit.Wrap("no").DrewSomething);
        Assert.False(Fit.Empty().DrewSomething);
    }

    [Fact]
    public void EmptyOccupiesNoSpaceAndGivesNoReason()
    {
        Fit plan = Fit.Empty();

        Assert.Equal(FitKind.Empty, plan.Type);
        Assert.Equal(Extent.Zero, plan.Size);
        Assert.Null(plan.WrapReason);
    }

    [Fact]
    public void FullRenderFromDimensionsMatchesFullRenderFromASize()
    {
        Fit plan = Fit.FullRender(3, 4);

        Assert.Equal(FitKind.FullRender, plan.Type);
        Assert.Equal(new Extent(3, 4), plan.Size);
        Assert.Null(plan.WrapReason);
        Assert.Equal(Fit.FullRender(new Extent(3, 4)), plan);
    }

    [Fact]
    public void PartialRenderFromDimensionsMatchesPartialRenderFromASize()
    {
        Fit plan = Fit.PartialRender(3, 4);

        Assert.Equal(FitKind.PartialRender, plan.Type);
        Assert.Equal(new Extent(3, 4), plan.Size);
        Assert.Null(plan.WrapReason);
        Assert.Equal(Fit.PartialRender(new Extent(3, 4)), plan);
    }

    [Fact]
    public void EachOutcomeAnswersYesToExactlyItsOwnQuestion()
    {
        Fit wrap = Fit.Wrap("no");
        Fit empty = Fit.Empty();
        Fit full = Fit.FullRender(1, 1);
        Fit partial = Fit.PartialRender(1, 1);

        Assert.Equal(new[] { true, false, false, false }, Flags(wrap));
        Assert.Equal(new[] { false, true, false, false }, Flags(empty));
        Assert.Equal(new[] { false, false, true, false }, Flags(full));
        Assert.Equal(new[] { false, false, false, true }, Flags(partial));
    }

    [Fact]
    public void WrapDescribesItselfWithItsReason()
    {
        Assert.Equal("Wrap (too narrow)", Fit.Wrap("too narrow").ToString());
    }

    [Fact]
    public void EmptyDescribesItselfByName()
    {
        Assert.Equal("Empty", Fit.Empty().ToString());
    }

    [Fact]
    public void RenderingOutcomesDescribeThemselvesWithTheirSize()
    {
        using CultureScope culture = CultureScope.Invariant();

        Assert.Equal("FullRender (Width: 1.500, Height: 2.250)", Fit.FullRender(1.5f, 2.25f).ToString());
        Assert.Equal("PartialRender (Width: 3.000, Height: 4.000)", Fit.PartialRender(3, 4).ToString());
    }

    private static bool[] Flags(Fit plan) => [plan.IsWrap, plan.IsEmpty, plan.IsFullRender, plan.IsPartialRender];
}
