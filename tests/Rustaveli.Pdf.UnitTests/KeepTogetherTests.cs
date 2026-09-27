namespace Rustaveli.Pdf.UnitTests;

public class KeepTogetherTests
{
    [Fact]
    public void ConvertsAPartialIntoADeferral()
    {
        KeepTogetherBlock element = new KeepTogetherBlock { Child = new SplittableBlock(unitCount: 4, unitHeight: 25) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 50));

        Assert.True(plan.IsDeferred);
    }

    [Fact]
    public void LeavesContentThatFitsAlone()
    {
        KeepTogetherBlock element = new KeepTogetherBlock { Child = new SplittableBlock(unitCount: 2, unitHeight: 25) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 50));

        Assert.True(plan.IsComplete);
    }

    [Fact]
    public void DrawsNothingWhenItWouldHaveToSplit()
    {
        KeepTogetherBlock element = new KeepTogetherBlock { Child = new SplittableBlock(unitCount: 4, unitHeight: 25) };

        RecordedPage page = LayoutHarness.Draw(element, new Extent(200, 50));

        Assert.Empty(page.Operations);
    }

    [Fact]
    public void MovesContentWholeToTheNextPage()
    {
        Document document = Document.Compose(container => container.Section(page =>
        {
            page.Trim = new Extent(200, 100);
            page.Body().Stack(column =>
            {
                column.Add().Compose(inner => inner.Slot().Child = new FixedBlock(10, 60));
                column.Add().KeepTogether().Compose(inner => inner.Slot().Child = new FixedBlock(10, 60));
            });
        }));

        RecordingSurface canvas = LayoutHarness.Render(document);

        // Each page also carries the white page background, so count only the content blocks.
        IEnumerable<RectangleOperation> firstPage = canvas.Page(1).Operations.OfType<RectangleOperation>().Where(r => r.Ink == TestInks.Black);
        IEnumerable<RectangleOperation> secondPage = canvas.Page(2).Operations.OfType<RectangleOperation>().Where(r => r.Ink == TestInks.Black);

        Assert.Equal(2, canvas.Pages.Count);
        Assert.Single(firstPage);
        Assert.Single(secondPage);
    }

    /// <summary>Content blocks drawn on each page: the fixed block in black, each splittable unit in blue.</summary>
    private static List<int> Blocks(RecordingSurface canvas) =>
        canvas.Pages.Select(page => page.Operations.OfType<RectangleOperation>().Count(rectangle => rectangle.Ink == TestInks.Black || rectangle.Ink == TestInks.Blue)).ToList();

    private static Document TwoItems(Func<IFrame, IFrame> keep, int units) => Document.Compose(container => container.Section(page =>
    {
        page.Trim = new Extent(200, 100);
        page.Body().Stack(column =>
        {
            column.Add().Compose(inner => inner.Slot().Child = new FixedBlock(10, 60));
            keep(column.Add()).Compose(inner => inner.Slot().Child = new SplittableBlock(unitCount: units, unitHeight: 25));
        });
    }));

    [Fact]
    public void WherePossibleMovesContentThatWouldFitAFreshPage()
    {
        RecordingSurface canvas = LayoutHarness.Render(TwoItems(frame => frame.KeepTogetherWherePossible(), units: 3));

        Assert.Equal([1, 3], Blocks(canvas));
    }

    [Fact]
    public void WherePossibleSplitsContentLongerThanAnyPage()
    {
        RecordingSurface canvas = LayoutHarness.Render(TwoItems(frame => frame.KeepTogetherWherePossible(), units: 6));

        Assert.Equal([2, 4, 1], Blocks(canvas));
        Assert.Throws<OversetException>(() => LayoutHarness.Render(TwoItems(frame => frame.KeepTogether(), units: 6)));
    }

    [Fact]
    public void WherePossibleSplitsAtTheTopOfAPage()
    {
        KeepTogetherBlock element = new KeepTogetherBlock { WherePossible = true, Child = new SplittableBlock(unitCount: 4, unitHeight: 25) };
        PlanContext context = LayoutHarness.Context();
        context.PageBody = new Extent(200, 50);

        Fit plan = element.Plan(new Extent(200, 50), context);

        Assert.True(plan.IsPartial);
        Approximately.Equal(new Extent(10, 50), plan.Size);
    }

    [Fact]
    public void WherePossibleDefersContentThatFitsAFreshPageAndDrawsNothingHere()
    {
        KeepTogetherBlock element = new KeepTogetherBlock { WherePossible = true, Child = new SplittableBlock(unitCount: 4, unitHeight: 25) };
        PlanContext context = LayoutHarness.Context();
        context.PageBody = new Extent(200, 100);

        Assert.True(element.Plan(new Extent(200, 50), context).IsDeferred);
        Assert.Empty(LayoutHarness.Draw(element, new Extent(200, 50), context).Operations);
        Assert.False(new KeepTogetherBlock().WherePossible);
    }
}
