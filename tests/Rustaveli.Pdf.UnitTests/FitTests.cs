namespace Rustaveli.Pdf.UnitTests;

public class FitTests
{
    [Fact]
    public void WrapCarriesItsReasonAndOccupiesNoSpace()
    {
        Fit plan = Fit.Defer("too narrow");

        Assert.True(plan.IsDeferred);
        Assert.Equal("too narrow", plan.DeferReason);
        Assert.Equal(Extent.Zero, plan.Size);
    }

    [Fact]
    public void OnlyRenderingOutcomesCountAsHavingDrawn()
    {
        Assert.True(Fit.Complete(new Extent(1, 1)).PlacesContent);
        Assert.True(Fit.Partial(new Extent(1, 1)).PlacesContent);
        Assert.False(Fit.Defer("no").PlacesContent);
        Assert.False(Fit.Nothing().PlacesContent);
    }

    [Fact]
    public void EmptyOccupiesNoSpaceAndGivesNoReason()
    {
        Fit plan = Fit.Nothing();

        Assert.Equal(FitKind.Nothing, plan.Kind);
        Assert.Equal(Extent.Zero, plan.Size);
        Assert.Null(plan.DeferReason);
    }

    [Fact]
    public void CompleteFromDimensionsMatchesCompleteFromASize()
    {
        Fit plan = Fit.Complete(3, 4);

        Assert.Equal(FitKind.Complete, plan.Kind);
        Assert.Equal(new Extent(3, 4), plan.Size);
        Assert.Null(plan.DeferReason);
        Assert.Equal(Fit.Complete(new Extent(3, 4)), plan);
    }

    [Fact]
    public void PartialFromDimensionsMatchesPartialFromASize()
    {
        Fit plan = Fit.Partial(3, 4);

        Assert.Equal(FitKind.Partial, plan.Kind);
        Assert.Equal(new Extent(3, 4), plan.Size);
        Assert.Null(plan.DeferReason);
        Assert.Equal(Fit.Partial(new Extent(3, 4)), plan);
    }

    [Fact]
    public void EachOutcomeAnswersYesToExactlyItsOwnQuestion()
    {
        Fit wrap = Fit.Defer("no");
        Fit empty = Fit.Nothing();
        Fit full = Fit.Complete(1, 1);
        Fit partial = Fit.Partial(1, 1);

        Assert.Equal(new[] { true, false, false, false }, Flags(wrap));
        Assert.Equal(new[] { false, true, false, false }, Flags(empty));
        Assert.Equal(new[] { false, false, true, false }, Flags(full));
        Assert.Equal(new[] { false, false, false, true }, Flags(partial));
    }

    [Fact]
    public void WrapDescribesItselfWithItsReason()
    {
        Assert.Equal("Defer: too narrow", Fit.Defer("too narrow").ToString());
    }

    [Fact]
    public void EmptyDescribesItselfByName()
    {
        Assert.Equal("Nothing", Fit.Nothing().ToString());
    }

    [Fact]
    public void RenderingOutcomesDescribeThemselvesWithTheirSize()
    {
        using CultureScope culture = CultureScope.DecimalComma();

        Assert.Equal("Complete, 1.5 × 2.25 pt", Fit.Complete(1.5f, 2.25f).ToString());
        Assert.Equal("Partial, 3 × 4 pt", Fit.Partial(3, 4).ToString());
    }

    private static bool[] Flags(Fit plan) => [plan.IsDeferred, plan.IsNothing, plan.IsComplete, plan.IsPartial];
}
