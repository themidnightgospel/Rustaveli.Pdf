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
}
