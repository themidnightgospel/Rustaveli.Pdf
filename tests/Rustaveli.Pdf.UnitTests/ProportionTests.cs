namespace Rustaveli.Pdf.UnitTests;

public class ProportionTests
{
    [Fact]
    public void DerivesHeightFromWidth()
    {
        ProportionBlock element = new ProportionBlock { Ratio = 2f, Fit = ProportionFit.Width };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 500));

        Approximately.Equal(new Extent(200, 100), plan.Size);
    }

    [Fact]
    public void DerivesWidthFromHeight()
    {
        ProportionBlock element = new ProportionBlock { Ratio = 2f, Fit = ProportionFit.Height };

        Fit plan = LayoutHarness.Measure(element, new Extent(500, 50));

        Approximately.Equal(new Extent(100, 50), plan.Size);
    }

    [Fact]
    public void FallsBackToHeightWhenWidthWouldOverflow()
    {
        // Fitting the 300pt width would need 300pt of height, but only 100 is available.
        ProportionBlock element = new ProportionBlock { Ratio = 1f, Fit = ProportionFit.Area };

        Fit plan = LayoutHarness.Measure(element, new Extent(300, 100));

        Approximately.Equal(new Extent(100, 100), plan.Size);
    }

    [Fact]
    public void FitAreaKeepsTheFullWidthWhenTheHeightAllowsIt()
    {
        ProportionBlock element = new ProportionBlock { Ratio = 2f, Fit = ProportionFit.Area };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 500));

        Approximately.Equal(new Extent(200, 100), plan.Size);
    }

    [Fact]
    public void AnUnrecognisedOptionFitsTheWidth()
    {
        ProportionBlock element = new ProportionBlock { Ratio = 2f, Fit = (ProportionFit)99 };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 500));

        Approximately.Equal(new Extent(200, 100), plan.Size);
    }

    [Fact]
    public void RejectsANonPositiveRatio()
    {
        ProportionBlock element = new ProportionBlock { Ratio = 0f };

        Fit plan = LayoutHarness.Measure(element, new Extent(100, 100));

        Assert.True(plan.IsDeferred);
        Assert.Contains("greater than zero", plan.DeferReason);
    }

    [Theory]
    [InlineData(100f, false)]
    [InlineData(99f, true)]
    public void WrapsWhenTheDerivedHeightDoesNotFit(float availableHeight, bool wraps)
    {
        // Fitting the full 200pt width at 2:1 needs exactly 100pt of height.
        ProportionBlock element = new ProportionBlock { Ratio = 2f, Fit = ProportionFit.Width };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, availableHeight));

        Assert.Equal(wraps, plan.IsDeferred);
    }

    [Fact]
    public void ReportsTheRatioBoxRatherThanTheChildSize()
    {
        ProportionBlock element = new ProportionBlock { Ratio = 2f, Child = new FixedBlock(20, 10) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 500));

        Assert.True(plan.IsComplete);
        Approximately.Equal(new Extent(200, 100), plan.Size);
    }

    [Fact]
    public void PassesTheChildsWrapThroughUnchanged()
    {
        // The child is measured against the ratio box, not the space offered to the element.
        FixedBlock child = new FixedBlock(300, 10);
        ProportionBlock element = new ProportionBlock { Ratio = 2f, Child = child };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 500));

        Assert.Equal(LayoutHarness.Measure(child, new Extent(200, 100)), plan);
    }

    [Fact]
    public void ReportsEmptyForAnExhaustedChild()
    {
        ProportionBlock element = new ProportionBlock { Ratio = 2f, Child = new ScriptedBlock(Fit.Nothing()) };

        Assert.True(LayoutHarness.Measure(element, new Extent(200, 500)).IsNothing);
    }

    [Fact]
    public void KeepsAPartialChildPartialAtTheRatioSize()
    {
        // The 200x100 box holds three of the four 30pt units.
        ProportionBlock element = new ProportionBlock { Ratio = 2f, Child = new SplittableBlock(unitCount: 4, unitHeight: 30) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 500));

        Assert.True(plan.IsPartial);
        Approximately.Equal(new Extent(200, 100), plan.Size);
    }

    [Fact]
    public void DrawsTheChildIntoTheRatioBox()
    {
        ProportionBlock element = new ProportionBlock { Ratio = 2f, Child = new PlaceholderBlock() };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 500));
        RectangleOperation block = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(new Extent(200, 100), block.Size);
    }

    [Fact]
    public void DrawsNothingWithoutContent()
    {
        ProportionBlock element = new ProportionBlock { Ratio = 2f };

        Assert.Empty(LayoutHarness.Draw(element, new Extent(200, 500)).Operations);
    }
}
