namespace Rustaveli.Pdf.UnitTests;

public class SpacePlanTests
{
    [Fact]
    public void WrapCarriesItsReasonAndOccupiesNoSpace()
    {
        SpacePlan plan = SpacePlan.Wrap("too narrow");

        Assert.True(plan.IsWrap);
        Assert.Equal("too narrow", plan.WrapReason);
        Assert.Equal(Size.Zero, plan.Size);
    }

    [Fact]
    public void OnlyRenderingOutcomesCountAsHavingDrawn()
    {
        Assert.True(SpacePlan.FullRender(new Size(1, 1)).DrewSomething);
        Assert.True(SpacePlan.PartialRender(new Size(1, 1)).DrewSomething);
        Assert.False(SpacePlan.Wrap("no").DrewSomething);
        Assert.False(SpacePlan.Empty().DrewSomething);
    }

    [Fact]
    public void EmptyOccupiesNoSpaceAndGivesNoReason()
    {
        SpacePlan plan = SpacePlan.Empty();

        Assert.Equal(SpacePlanType.Empty, plan.Type);
        Assert.Equal(Size.Zero, plan.Size);
        Assert.Null(plan.WrapReason);
    }

    [Fact]
    public void FullRenderFromDimensionsMatchesFullRenderFromASize()
    {
        SpacePlan plan = SpacePlan.FullRender(3, 4);

        Assert.Equal(SpacePlanType.FullRender, plan.Type);
        Assert.Equal(new Size(3, 4), plan.Size);
        Assert.Null(plan.WrapReason);
        Assert.Equal(SpacePlan.FullRender(new Size(3, 4)), plan);
    }

    [Fact]
    public void PartialRenderFromDimensionsMatchesPartialRenderFromASize()
    {
        SpacePlan plan = SpacePlan.PartialRender(3, 4);

        Assert.Equal(SpacePlanType.PartialRender, plan.Type);
        Assert.Equal(new Size(3, 4), plan.Size);
        Assert.Null(plan.WrapReason);
        Assert.Equal(SpacePlan.PartialRender(new Size(3, 4)), plan);
    }

    [Fact]
    public void EachOutcomeAnswersYesToExactlyItsOwnQuestion()
    {
        SpacePlan wrap = SpacePlan.Wrap("no");
        SpacePlan empty = SpacePlan.Empty();
        SpacePlan full = SpacePlan.FullRender(1, 1);
        SpacePlan partial = SpacePlan.PartialRender(1, 1);

        Assert.Equal(new[] { true, false, false, false }, Flags(wrap));
        Assert.Equal(new[] { false, true, false, false }, Flags(empty));
        Assert.Equal(new[] { false, false, true, false }, Flags(full));
        Assert.Equal(new[] { false, false, false, true }, Flags(partial));
    }

    [Fact]
    public void WrapDescribesItselfWithItsReason()
    {
        Assert.Equal("Wrap (too narrow)", SpacePlan.Wrap("too narrow").ToString());
    }

    [Fact]
    public void EmptyDescribesItselfByName()
    {
        Assert.Equal("Empty", SpacePlan.Empty().ToString());
    }

    [Fact]
    public void RenderingOutcomesDescribeThemselvesWithTheirSize()
    {
        using CultureScope culture = CultureScope.Invariant();

        Assert.Equal("FullRender (Width: 1.500, Height: 2.250)", SpacePlan.FullRender(1.5f, 2.25f).ToString());
        Assert.Equal("PartialRender (Width: 3.000, Height: 4.000)", SpacePlan.PartialRender(3, 4).ToString());
    }

    private static bool[] Flags(SpacePlan plan) => [plan.IsWrap, plan.IsEmpty, plan.IsFullRender, plan.IsPartialRender];
}
