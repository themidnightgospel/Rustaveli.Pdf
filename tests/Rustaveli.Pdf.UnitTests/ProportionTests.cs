namespace Rustaveli.Pdf.UnitTests;

public class ProportionTests
{
    [Fact]
    public void DerivesHeightFromWidth()
    {
        ProportionBlock block = new ProportionBlock { Ratio = 2f, Fit = ProportionFit.Width };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 500));

        Approximately.Equal(new Extent(200, 100), plan.Size);
    }

    [Fact]
    public void DerivesWidthFromHeight()
    {
        ProportionBlock block = new ProportionBlock { Ratio = 2f, Fit = ProportionFit.Height };

        Fit plan = LayoutHarness.Plan(block, new Extent(500, 50));

        Approximately.Equal(new Extent(100, 50), plan.Size);
    }

    [Fact]
    public void FallsBackToHeightWhenWidthWouldOverflow()
    {
        // Fitting the 300pt width would need 300pt of height, but only 100 is available.
        ProportionBlock block = new ProportionBlock { Ratio = 1f, Fit = ProportionFit.Area };

        Fit plan = LayoutHarness.Plan(block, new Extent(300, 100));

        Approximately.Equal(new Extent(100, 100), plan.Size);
    }

    [Fact]
    public void FitAreaKeepsTheFullWidthWhenTheHeightAllowsIt()
    {
        ProportionBlock block = new ProportionBlock { Ratio = 2f, Fit = ProportionFit.Area };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 500));

        Approximately.Equal(new Extent(200, 100), plan.Size);
    }

    [Fact]
    public void AnUnrecognisedOptionFitsTheWidth()
    {
        ProportionBlock block = new ProportionBlock { Ratio = 2f, Fit = (ProportionFit)99 };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 500));

        Approximately.Equal(new Extent(200, 100), plan.Size);
    }

    [Fact]
    public void RejectsANonPositiveRatio()
    {
        ProportionBlock block = new ProportionBlock { Ratio = 0f };

        Fit plan = LayoutHarness.Plan(block, new Extent(100, 100));

        Assert.True(plan.IsDeferred);
        Assert.Contains("greater than zero", plan.DeferReason);
    }

    [Theory]
    [InlineData(100f, false)]
    [InlineData(99f, true)]
    public void DefersWhenTheDerivedHeightDoesNotFit(float availableHeight, bool wraps)
    {
        // Fitting the full 200pt width at 2:1 needs exactly 100pt of height.
        ProportionBlock block = new ProportionBlock { Ratio = 2f, Fit = ProportionFit.Width };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, availableHeight));

        Assert.Equal(wraps, plan.IsDeferred);
    }

    [Fact]
    public void ReportsTheRatioBoxRatherThanTheChildSize()
    {
        ProportionBlock block = new ProportionBlock { Ratio = 2f, Child = new FixedBlock(20, 10) };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 500));

        Assert.True(plan.IsComplete);
        Approximately.Equal(new Extent(200, 100), plan.Size);
    }

    [Fact]
    public void PassesTheChildsDeferralThroughUnchanged()
    {
        // The child is measured against the ratio box, not the space offered to the block.
        FixedBlock child = new FixedBlock(300, 10);
        ProportionBlock block = new ProportionBlock { Ratio = 2f, Child = child };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 500));

        Assert.Equal(LayoutHarness.Plan(child, new Extent(200, 100)), plan);
    }

    [Fact]
    public void ReportsEmptyForAnExhaustedChild()
    {
        ProportionBlock block = new ProportionBlock { Ratio = 2f, Child = new ScriptedBlock(Fit.Nothing()) };

        Assert.True(LayoutHarness.Plan(block, new Extent(200, 500)).IsNothing);
    }

    [Fact]
    public void KeepsAPartialChildPartialAtTheRatioSize()
    {
        // The 200x100 box holds three of the four 30pt units.
        ProportionBlock block = new ProportionBlock { Ratio = 2f, Child = new SplittableBlock(unitCount: 4, unitHeight: 30) };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 500));

        Assert.True(plan.IsPartial);
        Approximately.Equal(new Extent(200, 100), plan.Size);
    }

    [Fact]
    public void DrawsTheChildIntoTheRatioBox()
    {
        ProportionBlock block = new ProportionBlock { Ratio = 2f, Child = new PlaceholderBlock() };

        RecordedPage page = LayoutHarness.Render(block, new Extent(200, 500));
        RectangleOperation box = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(new Extent(200, 100), box.Size);
    }

    [Fact]
    public void DrawsNothingWithoutContent()
    {
        ProportionBlock block = new ProportionBlock { Ratio = 2f };

        Assert.Empty(LayoutHarness.Render(block, new Extent(200, 500)).Operations);
    }
}
