namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Content that shows on some pages only, repeats on every page, or stops at the first.
/// </summary>
public class PagingControlTests
{
    private static ColumnsBlock Row(Block first, Block second)
    {
        ColumnsBlock row = new ColumnsBlock();
        row.Items.Add(new ColumnSlot { Sizing = ColumnSizing.Fixed, Value = 30, Child = first });
        row.Items.Add(new ColumnSlot { Sizing = ColumnSizing.Share, Value = 1, Child = second });
        return row;
    }

    private static int Reds(RecordedPage page) =>
        page.Operations.OfType<RectangleOperation>().Count(operation => operation.Ink == TestInks.Red);

    // ---- Repeating -----------------------------------------------------------------------------------------

    [Fact]
    public void ARepeatedColumnIsDrawnBesideItsNeighbourOnEveryPage()
    {
        ColumnsBlock row = Row(new RepeatBlock { Child = new FixedBlock(30, 20, TestInks.Red) }, new SplittableBlock(unitCount: 6, unitHeight: 30));
        Extent space = new Extent(200, 60);

        Assert.Equal(1, Reds(LayoutHarness.Draw(row, space)));
        Assert.Equal(1, Reds(LayoutHarness.Draw(row, space)));
        Assert.Equal(1, Reds(LayoutHarness.Draw(row, space)));
    }

    [Fact]
    public void ARepeatedColumnNeverKeepsTheRowGoingByItself()
    {
        ColumnsBlock row = Row(new RepeatBlock { Child = new FixedBlock(30, 20, TestInks.Red) }, new SplittableBlock(unitCount: 2, unitHeight: 30));
        Extent space = new Extent(200, 60);

        Assert.True(LayoutHarness.Measure(row, space).IsComplete);
        LayoutHarness.Draw(row, space);

        Assert.True(LayoutHarness.Measure(row, space).IsNothing);
        Assert.Equal(0, Reds(LayoutHarness.Draw(row, space)));
    }

    [Fact]
    public void ARepeatedColumnStandsInItsOwnSlotOnLaterPages()
    {
        ColumnsBlock row = Row(new SplittableBlock(unitCount: 6, unitHeight: 30), new RepeatBlock { Child = new FixedBlock(30, 20, TestInks.Red) });
        Extent space = new Extent(200, 60);

        LayoutHarness.Draw(row, space);
        RectangleOperation later = LayoutHarness.Draw(row, space).Operations.OfType<RectangleOperation>().Single(operation => operation.Ink == TestInks.Red);

        Approximately.Equal(new Offset(30, 0), later.Position);
    }

    [Fact]
    public void ARepeatedColumnTooTallForALaterPageIsLeftOutWhileItsNeighbourGoesOn()
    {
        SplittableBlock neighbour = new SplittableBlock(unitCount: 6, unitHeight: 30);
        ColumnsBlock row = Row(new RepeatBlock { Child = new FixedBlock(30, 50, TestInks.Red) }, neighbour);

        Assert.Equal(1, Reds(LayoutHarness.Draw(row, new Extent(200, 60))));
        Assert.Equal(0, Reds(LayoutHarness.Draw(row, new Extent(200, 40))));
        Assert.Equal(3, neighbour.Remaining);
    }

    [Fact]
    public void RepeatedContentStartsAgainFromItsBeginning()
    {
        // Two units fit on each page, so the repeated content is drawn whole and then starts over.
        SplittableBlock repeated = new SplittableBlock(unitCount: 2, unitHeight: 30);
        ColumnsBlock row = Row(new RepeatBlock { Child = repeated }, new SplittableBlock(unitCount: 6, unitHeight: 30));
        Extent space = new Extent(200, 60);

        LayoutHarness.Draw(row, space);
        Assert.Equal(2, repeated.Remaining);

        LayoutHarness.Draw(row, space);
        Assert.Equal(2, repeated.Remaining);
    }

    [Fact]
    public void RepeatedContentTooLongForOnePageContinuesBeforeStartingAgain()
    {
        SplittableBlock repeated = new SplittableBlock(unitCount: 3, unitHeight: 30);
        RepeatBlock element = new RepeatBlock { Child = repeated };
        Extent space = new Extent(200, 60);

        LayoutHarness.Draw(element, space);
        Assert.Equal(1, repeated.Remaining);

        LayoutHarness.Draw(element, space);
        Assert.Equal(3, repeated.Remaining);
    }

    [Fact]
    public void RepeatedContentKeepsWhatCountsAcrossTheDocument()
    {
        // Starting again is a page's reset, not a new document: content shown once stays shown once.
        RepeatBlock element = new RepeatBlock { Child = new OnceBlock { Child = new FixedBlock(30, 20, TestInks.Red) } };
        Extent space = new Extent(200, 60);

        Assert.Equal(1, Reds(LayoutHarness.Draw(element, space)));
        Assert.Equal(0, Reds(LayoutHarness.Draw(element, space)));
    }

    [Fact]
    public void RepeatedContentThatDoesNotFitIsNotDrawn()
    {
        RepeatBlock element = new RepeatBlock { Child = new FixedBlock(300, 20, TestInks.Red) };

        Assert.Empty(LayoutHarness.Draw(element, new Extent(200, 60)).Operations);
        Assert.Empty(LayoutHarness.Draw(new RepeatBlock(), new Extent(200, 60)).Operations);
    }

    [Fact]
    public void AWrapperAroundRepeatedContentRepeatsWithIt()
    {
        Assert.True(new FillBlock { Child = new RepeatBlock() }.Repeats);
        Assert.False(new FillBlock { Child = new FixedBlock(1, 1) }.Repeats);
        Assert.False(new FillBlock().Repeats);
    }

    [Fact]
    public void RepeatOnEachPageRepeatsTheFrame()
    {
        Block root = LayoutHarness.Build(frame => frame.RepeatOnEachPage().Compose(inner => { }));

        Assert.True(root.Repeats);
    }

    // ---- Discarding the overset ----------------------------------------------------------------------------

    [Fact]
    public void WhatDoesNotFitIsDiscarded()
    {
        SplittableBlock content = new SplittableBlock(unitCount: 5, unitHeight: 30);
        DiscardOversetBlock element = new DiscardOversetBlock { Child = content };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 60));
        LayoutHarness.Draw(element, new Extent(200, 60));

        Assert.True(plan.IsComplete);
        Approximately.Equal(60f, plan.Size.Height);
        Assert.Equal(3, content.Remaining);
    }

    [Fact]
    public void ContentThatFitsNowhereTakesNoRoom()
    {
        DiscardOversetBlock element = new DiscardOversetBlock { Child = new FixedBlock(300, 20, TestInks.Red) };

        Fit plan = LayoutHarness.Measure(element, new Extent(200, 60));

        Assert.True(plan.IsComplete);
        Assert.Equal(Extent.Zero, plan.Size);
        Assert.Empty(LayoutHarness.Draw(element, new Extent(200, 60)).Operations);
    }

    [Fact]
    public void ContentThatFitsIsDrawnAsItIs()
    {
        DiscardOversetBlock element = new DiscardOversetBlock { Child = new FixedBlock(30, 20, TestInks.Red) };

        Assert.True(LayoutHarness.Measure(element, new Extent(200, 60)).IsComplete);
        Assert.Equal(1, Reds(LayoutHarness.Draw(element, new Extent(200, 60))));
        Assert.Empty(LayoutHarness.Draw(new DiscardOversetBlock(), new Extent(200, 60)).Operations);
    }

    [Fact]
    public void ContentUsedUpStaysUsedUp()
    {
        SplittableBlock content = new SplittableBlock(unitCount: 1, unitHeight: 30);
        DiscardOversetBlock element = new DiscardOversetBlock { Child = content };

        LayoutHarness.Draw(element, new Extent(200, 60));

        Assert.True(LayoutHarness.Measure(element, new Extent(200, 60)).IsNothing);
    }

    [Fact]
    public void ADocumentStopsAtTheFirstPageOfDiscardedOverset()
    {
        Document document = Document.Compose(container => container.Section(page =>
        {
            page.Trim = new Extent(100, 60);
            page.Margins = Sides.All(0);
            page.Body().DiscardOverset().Compose(inner => inner.Slot().Child = new SplittableBlock(unitCount: 6, unitHeight: 30));
        }));

        Assert.Single(LayoutHarness.Render(document).Pages);
    }

    // ---- Showing on some pages -----------------------------------------------------------------------------

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void AConditionIsAskedOfEachPage(int folio, bool shown)
    {
        WhenBlock element = new WhenBlock { OnPage = page => page.IsOdd, Child = new FixedBlock(30, 20, TestInks.Red) };
        PlanContext context = LayoutHarness.Context(new Pagination { Folio = folio });

        Assert.Equal(shown ? 1 : 0, Reds(LayoutHarness.Draw(element, new Extent(200, 60), context)));
        Approximately.Equal(shown ? 20f : 0f, LayoutHarness.Measure(element, new Extent(200, 60), context).Size.Height);
    }

    [Fact]
    public void ThePageCountIsUnknownWhileThePagesAreCounted()
    {
        List<PageFacts> seen = [];
        WhenBlock element = new WhenBlock { OnPage = page => { seen.Add(page); return true; }, Child = new FixedBlock(30, 20) };

        LayoutHarness.Measure(element, new Extent(200, 60), LayoutHarness.Context(new Pagination { Folio = 2, PageCount = 5 }));
        LayoutHarness.Measure(element, new Extent(200, 60), LayoutHarness.Context(new Pagination { Folio = 2, PageCount = 5, IsPageCountKnown = true }));

        Assert.Equal([new PageFacts(2, null), new PageFacts(2, 5)], seen);
    }

    [Fact]
    public void AFixedConditionStillApplies()
    {
        WhenBlock element = new WhenBlock { Condition = false, OnPage = page => true, Child = new FixedBlock(30, 20, TestInks.Red) };

        Assert.Equal(0, Reds(LayoutHarness.Draw(element, new Extent(200, 60))));
    }

    [Fact]
    public void AFootShowsOnlyOnTheLastPage()
    {
        Document document = Document.Compose(container => container.Section(page =>
        {
            page.Trim = new Extent(100, 90);
            page.Margins = Sides.All(0);
            page.RunningFoot().When(facts => facts.IsLast).Text("End");
            page.Body().Compose(inner => inner.Slot().Child = new SplittableBlock(unitCount: 5, unitHeight: 30));
        }));

        List<RecordedPage> pages = LayoutHarness.Render(document).Pages;

        Assert.True(pages.Count > 1);
        Assert.All(pages.Take(pages.Count - 1), shown => Assert.DoesNotContain("End", shown.Content));
        Assert.Contains("End", pages[pages.Count - 1].Content);
    }

    [Fact]
    public void WhenNeedsACondition() =>
        Assert.Throws<ArgumentNullException>(() => LayoutHarness.Build(frame => frame.When((Func<PageFacts, bool>)null!)));

    [Theory]
    [InlineData(1, null, true, false, true)]
    [InlineData(2, 2, false, true, false)]
    [InlineData(3, 4, false, false, true)]
    public void FactsAboutAPage(int folio, int? pageCount, bool first, bool last, bool odd)
    {
        PageFacts facts = new PageFacts(folio, pageCount);

        Assert.Equal(first, facts.IsFirst);
        Assert.Equal(last, facts.IsLast);
        Assert.Equal(odd, facts.IsOdd);
    }
}
