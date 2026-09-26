namespace Rustaveli.Pdf.UnitTests;

public class FlowControlTests
{
    [Fact]
    public void ShowIfHidesContentWhenFalse()
    {
        ShowIfElement element = new ShowIfElement { Condition = false, Child = new FixedElement(50, 50) };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 200));

        Assert.Empty(page.Operations);
        Approximately.Equal(0f, LayoutHarness.Measure(element, new Extent(200, 200)).Size.Height);
    }

    [Fact]
    public void HiddenItemsDoNotConsumeColumnSpacing()
    {
        // A hidden item must leave no trace. If it still counted as drawn content, the column would insert a
        // gap either side of nothing, and toggling a section on and off would shift the whole layout.
        ColumnElement column = new ColumnElement { Spacing = 10 };
        column.Items.Add(new FixedElement(10, 20));
        column.Items.Add(new ShowIfElement { Condition = false, Child = new FixedElement(10, 50) });
        column.Items.Add(new FixedElement(10, 20));

        Fit plan = LayoutHarness.Measure(column, new Extent(200, 500));

        // Two visible items, so exactly one 10pt gap.
        Approximately.Equal(50f, plan.Size.Height);
    }

    [Fact]
    public void SkipOnceLeavesNoGapWhileItIsSuppressed()
    {
        // Unlike a hidden item, this one must still be drawn so its state advances — but it must not be spaced.
        ColumnElement column = new ColumnElement { Spacing = 10 };
        column.Items.Add(new FixedElement(10, 20));
        column.Items.Add(new SkipOnceElement { Child = new FixedElement(10, 50) });
        column.Items.Add(new FixedElement(10, 20));

        Fit plan = LayoutHarness.Measure(column, new Extent(200, 500));

        Approximately.Equal(50f, plan.Size.Height);
    }

    [Fact]
    public void ShowIfKeepsContentWhenTrue()
    {
        ShowIfElement element = new ShowIfElement { Condition = true, Child = new FixedElement(50, 50) };

        Assert.Single(LayoutHarness.Draw(element, new Extent(200, 200)).Operations);
    }

    [Fact]
    public void ShowOnceDrawsOnlyTheFirstTime()
    {
        ShowOnceElement element = new ShowOnceElement { Child = new FixedElement(50, 50) };
        Extent space = new Extent(200, 200);

        Assert.Single(LayoutHarness.Draw(element, space).Operations);
        Assert.Empty(LayoutHarness.Draw(element, space).Operations);
    }

    [Fact]
    public void ShowOnceSurvivesAPerPageReset()
    {
        // Headers are reset between pages; content marked "show once" must not reappear because of it.
        ShowOnceElement element = new ShowOnceElement { Child = new FixedElement(50, 50) };
        Extent space = new Extent(200, 200);

        LayoutHarness.Draw(element, space);
        element.ResetState(includeDocumentProgress: false);

        Assert.Empty(LayoutHarness.Draw(element, space).Operations);
    }

    [Fact]
    public void ShowOnceReturnsAfterAFullReset()
    {
        ShowOnceElement element = new ShowOnceElement { Child = new FixedElement(50, 50) };
        Extent space = new Extent(200, 200);

        LayoutHarness.Draw(element, space);
        element.ResetState();

        Assert.Single(LayoutHarness.Draw(element, space).Operations);
    }

    [Fact]
    public void SkipOnceSuppressesOnlyTheFirstOccurrence()
    {
        SkipOnceElement element = new SkipOnceElement { Child = new FixedElement(50, 50) };
        Extent space = new Extent(200, 200);

        Assert.Empty(LayoutHarness.Draw(element, space).Operations);
        Assert.Single(LayoutHarness.Draw(element, space).Operations);
    }

    [Fact]
    public void PageBreakClaimsTheRemainingHeight()
    {
        PageBreakElement element = new PageBreakElement();

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 150));

        Assert.True(plan.IsPartialRender);
        Approximately.Equal(150f, plan.Size.Height);
    }

    [Fact]
    public void PageBreakIsSpentAfterDrawing()
    {
        PageBreakElement element = new PageBreakElement();
        Extent space = new Extent(200, 150);

        LayoutHarness.Draw(element, space);

        Assert.True(LayoutHarness.Measure(element, space).IsEmpty);
    }

    [Fact]
    public void ShowIfMeasuresItsContentWhenTrue()
    {
        ShowIfElement element = new ShowIfElement { Condition = true, Child = new FixedElement(50, 30) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 200));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(new Extent(50, 30), plan.Size);
    }

    [Fact]
    public void ShowOnceMeasuresItsContentUntilItHasRendered()
    {
        ShowOnceElement element = new ShowOnceElement { Child = new FixedElement(50, 30) };
        Extent space = new Extent(200, 200);

        Approximately.Equal(new Extent(50, 30), LayoutHarness.Measure(element, space).Size);

        LayoutHarness.Draw(element, space);

        Assert.True(LayoutHarness.Measure(element, space).IsEmpty);
    }

    [Fact]
    public void SkipOnceSurvivesAPerPageReset()
    {
        // A "continued" marker in a header is reset with the header each page, but must not start skipping again.
        SkipOnceElement element = new SkipOnceElement { Child = new FixedElement(50, 50) };
        Extent space = new Extent(200, 200);

        LayoutHarness.Draw(element, space);
        element.ResetState(includeDocumentProgress: false);

        Assert.Single(LayoutHarness.Draw(element, space).Operations);
    }

    [Fact]
    public void SkipOnceSkipsAgainAfterAFullReset()
    {
        SkipOnceElement element = new SkipOnceElement { Child = new FixedElement(50, 50) };
        Extent space = new Extent(200, 200);

        LayoutHarness.Draw(element, space);
        element.ResetState();

        Assert.Empty(LayoutHarness.Draw(element, space).Operations);
    }
}
