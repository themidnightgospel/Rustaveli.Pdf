namespace Rustaveli.Pdf.UnitTests;

public class KeepTogetherTests
{
    [Fact]
    public void ConvertsAPartialIntoADeferral()
    {
        KeepTogetherBlock block = new KeepTogetherBlock { Child = new SplittableBlock(unitCount: 4, unitHeight: 25) };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 50));

        Assert.True(plan.IsDeferred);
    }

    [Fact]
    public void LeavesContentThatFitsAlone()
    {
        KeepTogetherBlock block = new KeepTogetherBlock { Child = new SplittableBlock(unitCount: 2, unitHeight: 25) };

        Fit plan = LayoutHarness.Plan(block, new Extent(200, 50));

        Assert.True(plan.IsComplete);
    }

    [Fact]
    public void DrawsNothingWhenItWouldHaveToSplit()
    {
        KeepTogetherBlock block = new KeepTogetherBlock { Child = new SplittableBlock(unitCount: 4, unitHeight: 25) };

        RecordedPage page = LayoutHarness.Render(block, new Extent(200, 50));

        Assert.Empty(page.Operations);
    }

    [Fact]
    public void MovesContentWholeToTheNextPage()
    {
        Document document = Document.Compose(frame => frame.Section(page =>
        {
            page.Trim = new Extent(200, 100);
            page.Body().Stack(column =>
            {
                column.Add().Compose(inner => inner.Slot().Child = new FixedBlock(10, 60));
                column.Add().KeepTogether().Compose(inner => inner.Slot().Child = new FixedBlock(10, 60));
            });
        }));

        RecordingSurface surface = LayoutHarness.Render(document);

        // Each page also carries the white page background, so count only the content blocks.
        IEnumerable<RectangleOperation> firstPage = surface.Page(1).Operations.OfType<RectangleOperation>().Where(r => r.Ink == TestInks.Black);
        IEnumerable<RectangleOperation> secondPage = surface.Page(2).Operations.OfType<RectangleOperation>().Where(r => r.Ink == TestInks.Black);

        Assert.Equal(2, surface.Pages.Count);
        Assert.Single(firstPage);
        Assert.Single(secondPage);
    }

    /// <summary>Content blocks drawn on each page: the fixed block in black, each splittable unit in blue.</summary>
    private static List<int> Blocks(RecordingSurface surface) =>
        surface.Pages.Select(page => page.Operations.OfType<RectangleOperation>().Count(rectangle => rectangle.Ink == TestInks.Black || rectangle.Ink == TestInks.Blue)).ToList();

    private static Document TwoItems(Func<IFrame, IFrame> keep, int units) => Document.Compose(frame => frame.Section(page =>
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
        RecordingSurface surface = LayoutHarness.Render(TwoItems(frame => frame.KeepTogetherWherePossible(), units: 3));

        Assert.Equal([1, 3], Blocks(surface));
    }

    [Fact]
    public void WherePossibleSplitsContentLongerThanAnyPage()
    {
        RecordingSurface surface = LayoutHarness.Render(TwoItems(frame => frame.KeepTogetherWherePossible(), units: 6));

        Assert.Equal([2, 4, 1], Blocks(surface));
        Assert.Throws<OversetException>(() => LayoutHarness.Render(TwoItems(frame => frame.KeepTogether(), units: 6)));
    }

    [Fact]
    public void WherePossibleSplitsContentThatAFreshPageOffersTooLittleRoomFor()
    {
        // The content is shorter than the page body, but the inset leaves less than that on any page. Moving it on
        // can never help, so it is split rather than refused.
        Document document = Document.Compose(frame => frame.Section(page =>
        {
            page.Trim = new Extent(200, 100);
            page.Body().Inset(20).KeepTogetherWherePossible().Compose(inner => inner.Slot().Child = new SplittableBlock(unitCount: 4, unitHeight: 22));
        }));

        RecordingSurface surface = LayoutHarness.Render(document);

        Assert.Equal([2, 2], Blocks(surface));
    }

    [Fact]
    public void WherePossibleMovesContentOnBeforeSplittingItWhereItStartsThePage()
    {
        // Behind other content the move is still tried: only the page it then starts proves it can never fit whole.
        Document document = Document.Compose(frame => frame.Section(page =>
        {
            page.Trim = new Extent(200, 100);
            page.Body().Stack(column =>
            {
                column.Add().Compose(inner => inner.Slot().Child = new FixedBlock(10, 30));
                column.Add().InsetVertical(20).KeepTogetherWherePossible().Compose(inner => inner.Slot().Child = new SplittableBlock(unitCount: 4, unitHeight: 22));
            });
        }));

        RecordingSurface surface = LayoutHarness.Render(document);

        Assert.Equal([1, 2, 2], Blocks(surface));
    }

    [Fact]
    public void WherePossibleSplitsAtTheTopOfAPage()
    {
        KeepTogetherBlock block = new KeepTogetherBlock { WherePossible = true, Child = new SplittableBlock(unitCount: 4, unitHeight: 25) };
        PlanContext context = LayoutHarness.Context();
        context.PageBody = new Extent(200, 50);

        Fit plan = block.Plan(new Extent(200, 50), context);

        Assert.True(plan.IsPartial);
        Approximately.Equal(new Extent(10, 50), plan.Size);
    }

    [Fact]
    public void WherePossibleDefersContentThatFitsAFreshPageAndDrawsNothingHere()
    {
        KeepTogetherBlock block = new KeepTogetherBlock { WherePossible = true, Child = new SplittableBlock(unitCount: 4, unitHeight: 25) };
        PlanContext context = LayoutHarness.Context();
        context.PageBody = new Extent(200, 100);

        Assert.True(block.Plan(new Extent(200, 50), context).IsDeferred);
        Assert.Empty(LayoutHarness.Render(block, new Extent(200, 50), context).Operations);
        Assert.False(new KeepTogetherBlock().WherePossible);
    }
}
