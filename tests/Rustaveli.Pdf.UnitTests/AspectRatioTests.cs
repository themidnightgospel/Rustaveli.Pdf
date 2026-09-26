namespace Rustaveli.Pdf.UnitTests;

public class AspectRatioTests
{
    [Fact]
    public void DerivesHeightFromWidth()
    {
        AspectRatioElement element = new AspectRatioElement { Ratio = 2f, Option = AspectRatioOption.FitWidth };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 500));

        Approximately.Equal(new Size(200, 100), plan.Size);
    }

    [Fact]
    public void DerivesWidthFromHeight()
    {
        AspectRatioElement element = new AspectRatioElement { Ratio = 2f, Option = AspectRatioOption.FitHeight };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(500, 50));

        Approximately.Equal(new Size(100, 50), plan.Size);
    }

    [Fact]
    public void FallsBackToHeightWhenWidthWouldOverflow()
    {
        // Fitting the 300pt width would need 300pt of height, but only 100 is available.
        AspectRatioElement element = new AspectRatioElement { Ratio = 1f, Option = AspectRatioOption.FitArea };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(300, 100));

        Approximately.Equal(new Size(100, 100), plan.Size);
    }

    [Fact]
    public void FitAreaKeepsTheFullWidthWhenTheHeightAllowsIt()
    {
        AspectRatioElement element = new AspectRatioElement { Ratio = 2f, Option = AspectRatioOption.FitArea };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 500));

        Approximately.Equal(new Size(200, 100), plan.Size);
    }

    [Fact]
    public void AnUnrecognisedOptionFitsTheWidth()
    {
        AspectRatioElement element = new AspectRatioElement { Ratio = 2f, Option = (AspectRatioOption)99 };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 500));

        Approximately.Equal(new Size(200, 100), plan.Size);
    }

    [Fact]
    public void RejectsANonPositiveRatio()
    {
        AspectRatioElement element = new AspectRatioElement { Ratio = 0f };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(100, 100));

        Assert.True(plan.IsWrap);
        Assert.Contains("greater than zero", plan.WrapReason);
    }

    [Theory]
    [InlineData(100f, false)]
    [InlineData(99f, true)]
    public void WrapsWhenTheDerivedHeightDoesNotFit(float availableHeight, bool wraps)
    {
        // Fitting the full 200pt width at 2:1 needs exactly 100pt of height.
        AspectRatioElement element = new AspectRatioElement { Ratio = 2f, Option = AspectRatioOption.FitWidth };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, availableHeight));

        Assert.Equal(wraps, plan.IsWrap);
    }

    [Fact]
    public void ReportsTheRatioBoxRatherThanTheChildSize()
    {
        AspectRatioElement element = new AspectRatioElement { Ratio = 2f, Child = new FixedElement(20, 10) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 500));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(new Size(200, 100), plan.Size);
    }

    [Fact]
    public void PassesTheChildsWrapThroughUnchanged()
    {
        // The child is measured against the ratio box, not the space offered to the element.
        FixedElement child = new FixedElement(300, 10);
        AspectRatioElement element = new AspectRatioElement { Ratio = 2f, Child = child };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 500));

        Assert.Equal(LayoutHarness.Measure(child, new Size(200, 100)), plan);
    }

    [Fact]
    public void ReportsEmptyForAnExhaustedChild()
    {
        AspectRatioElement element = new AspectRatioElement { Ratio = 2f, Child = new ScriptedElement(SpacePlan.Empty()) };

        Assert.True(LayoutHarness.Measure(element, new Size(200, 500)).IsEmpty);
    }

    [Fact]
    public void KeepsAPartialChildPartialAtTheRatioSize()
    {
        // The 200x100 box holds three of the four 30pt units.
        AspectRatioElement element = new AspectRatioElement { Ratio = 2f, Child = new SplittableElement(unitCount: 4, unitHeight: 30) };

        SpacePlan plan = LayoutHarness.Measure(element, new Size(200, 500));

        Assert.True(plan.IsPartialRender);
        Approximately.Equal(new Size(200, 100), plan.Size);
    }

    [Fact]
    public void DrawsTheChildIntoTheRatioBox()
    {
        AspectRatioElement element = new AspectRatioElement { Ratio = 2f, Child = new PlaceholderElement() };

        RecordedPage page = LayoutHarness.Draw(element, new Size(200, 500));
        RectangleOperation block = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(new Size(200, 100), block.Size);
    }

    [Fact]
    public void DrawsNothingWithoutContent()
    {
        AspectRatioElement element = new AspectRatioElement { Ratio = 2f };

        Assert.Empty(LayoutHarness.Draw(element, new Size(200, 500)).Operations);
    }
}
