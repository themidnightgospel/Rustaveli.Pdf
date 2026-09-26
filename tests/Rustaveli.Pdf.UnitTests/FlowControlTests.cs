namespace Rustaveli.Pdf.UnitTests;

public class FlowControlTests
{
    [Fact]
    public void WhenFalseHidesContent()
    {
        WhenBlock element = new WhenBlock { Condition = false, Child = new FixedBlock(50, 50) };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(page.Operations);
        Approximately.Equal(0f, LayoutHarness.Measure(element, new Extent(200, 200)).Size.Height);
    }

    [Fact]
    public void HiddenItemsDoNotConsumeColumnSpacing()
    {
        // A hidden item must leave no trace. If it still counted as drawn content, the column would insert a
        // gap either side of nothing, and toggling a section on and off would shift the whole layout.
        StackBlock column = new StackBlock { SpaceBetween = 10 };
        column.Items.Add(new FixedBlock(10, 20));
        column.Items.Add(new WhenBlock { Condition = false, Child = new FixedBlock(10, 50) });
        column.Items.Add(new FixedBlock(10, 20));

        Fit plan = LayoutHarness.Measure(column, new Extent(200, 500));

        // Two visible items, so exactly one 10pt gap.
        Approximately.Equal(50f, plan.Size.Height);
    }

    [Fact]
    public void SkipFirstLeavesNoGapWhileItIsSuppressed()
    {
        // Unlike a hidden item, this one must still be drawn so its state advances — but it must not be spaced.
        StackBlock column = new StackBlock { SpaceBetween = 10 };
        column.Items.Add(new FixedBlock(10, 20));
        column.Items.Add(new SkipFirstBlock { Child = new FixedBlock(10, 50) });
        column.Items.Add(new FixedBlock(10, 20));

        Fit plan = LayoutHarness.Measure(column, new Extent(200, 500));

        Approximately.Equal(50f, plan.Size.Height);
    }

    [Fact]
    public void WhenTrueKeepsContent()
    {
        WhenBlock element = new WhenBlock { Condition = true, Child = new FixedBlock(50, 50) };

        Assert.Single(LayoutHarness.Draw(element, new Extent(200, 200)).Operations);
    }

    [Fact]
    public void OnceDrawsOnlyTheFirstTime()
    {
        OnceBlock element = new OnceBlock { Child = new FixedBlock(50, 50) };
        Extent space = new Extent(200, 200);

        Assert.Single(LayoutHarness.Draw(element, space).Operations);
        Assert.Empty(LayoutHarness.Draw(element, space).Operations);
    }

    [Fact]
    public void OnceSurvivesAPerPageReset()
    {
        // Headers are reset between pages; content marked "show once" must not reappear because of it.
        OnceBlock element = new OnceBlock { Child = new FixedBlock(50, 50) };
        Extent space = new Extent(200, 200);

        LayoutHarness.Draw(element, space);
        element.ResetState(includeDocumentProgress: false);

        Assert.Empty(LayoutHarness.Draw(element, space).Operations);
    }

    [Fact]
    public void OnceReturnsAfterAFullReset()
    {
        OnceBlock element = new OnceBlock { Child = new FixedBlock(50, 50) };
        Extent space = new Extent(200, 200);

        LayoutHarness.Draw(element, space);
        element.ResetState();

        Assert.Single(LayoutHarness.Draw(element, space).Operations);
    }

    [Fact]
    public void SkipFirstSuppressesOnlyTheFirstOccurrence()
    {
        SkipFirstBlock element = new SkipFirstBlock { Child = new FixedBlock(50, 50) };
        Extent space = new Extent(200, 200);

        Assert.Empty(LayoutHarness.Draw(element, space).Operations);
        Assert.Single(LayoutHarness.Draw(element, space).Operations);
    }

    [Fact]
    public void NewPageClaimsTheRemainingHeight()
    {
        NewPageBlock element = new NewPageBlock();

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 150));

        Assert.True(plan.IsPartial);
        Approximately.Equal(150f, plan.Size.Height);
    }

    [Fact]
    public void NewPageIsSpentAfterDrawing()
    {
        NewPageBlock element = new NewPageBlock();
        Extent space = new Extent(200, 150);

        LayoutHarness.Draw(element, space);

        Assert.True(LayoutHarness.Measure(element, space).IsNothing);
    }

    [Fact]
    public void WhenTruePlansItsContent()
    {
        WhenBlock element = new WhenBlock { Condition = true, Child = new FixedBlock(50, 30) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Assert.True(plan.IsComplete);
        Approximately.Equal(new Extent(50, 30), plan.Size);
    }

    [Fact]
    public void OnceMeasuresItsContentUntilItHasRendered()
    {
        OnceBlock element = new OnceBlock { Child = new FixedBlock(50, 30) };
        Extent space = new Extent(200, 200);

        Approximately.Equal(new Extent(50, 30), LayoutHarness.Measure(element, space).Size);

        LayoutHarness.Draw(element, space);

        Assert.True(LayoutHarness.Measure(element, space).IsNothing);
    }

    [Fact]
    public void SkipFirstSurvivesAPerPageReset()
    {
        // A "continued" marker in a header is reset with the header each page, but must not start skipping again.
        SkipFirstBlock element = new SkipFirstBlock { Child = new FixedBlock(50, 50) };
        Extent space = new Extent(200, 200);

        LayoutHarness.Draw(element, space);
        element.ResetState(includeDocumentProgress: false);

        Assert.Single(LayoutHarness.Draw(element, space).Operations);
    }

    [Fact]
    public void SkipFirstSkipsAgainAfterAFullReset()
    {
        SkipFirstBlock element = new SkipFirstBlock { Child = new FixedBlock(50, 50) };
        Extent space = new Extent(200, 200);

        LayoutHarness.Draw(element, space);
        element.ResetState();

        Assert.Empty(LayoutHarness.Draw(element, space).Operations);
    }
}
