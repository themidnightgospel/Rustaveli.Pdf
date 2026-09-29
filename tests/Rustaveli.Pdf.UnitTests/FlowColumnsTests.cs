namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// A story flowing through columns 45 points wide with a gutter of 10, in units 30 points tall.
/// </summary>
public class FlowColumnsTests
{
    private static FlowColumnsBlock Columns(SplittableBlock story, bool balanced = false, Block? between = null) =>
        new FlowColumnsBlock { Count = 2, Gutter = 10, Balanced = balanced, Story = story, Between = between };

    private static List<Offset> Units(RecordedPage page) =>
        page.Operations.OfType<RectangleOperation>().Where(operation => operation.Size.Height == 30).Select(operation => operation.Position).ToList();

    [Fact]
    public void TheStoryRunsDownOneColumnAndOnIntoTheNext()
    {
        FlowColumnsBlock columns = Columns(new SplittableBlock(unitCount: 5, unitHeight: 30));
        Extent space = new Extent(100, 90);

        Fit plan = LayoutHarness.Measure(columns, space);
        List<Offset> units = Units(LayoutHarness.Draw(columns, space));

        Assert.True(plan.IsComplete);
        Assert.Equal(new Extent(100, 90), plan.Size);
        Assert.Equal([new Offset(0, 0), new Offset(0, 30), new Offset(0, 60), new Offset(55, 0), new Offset(55, 30)], units);
    }

    [Fact]
    public void AStoryLongerThanTheColumnsGoesOnToTheNextPage()
    {
        SplittableBlock story = new SplittableBlock(unitCount: 8, unitHeight: 30);
        FlowColumnsBlock columns = Columns(story);
        Extent space = new Extent(100, 90);

        Assert.True(LayoutHarness.Measure(columns, space).IsPartial);
        Assert.Equal(6, Units(LayoutHarness.Draw(columns, space)).Count);
        Assert.Equal(2, story.Remaining);

        Fit next = LayoutHarness.Measure(columns, space);
        Assert.True(next.IsComplete);
        Assert.Equal(60f, next.Size.Height);
        Assert.Equal(2, Units(LayoutHarness.Draw(columns, space)).Count);

        Assert.True(LayoutHarness.Measure(columns, space).IsNothing);
    }

    [Fact]
    public void MeasuringLeavesTheStoryWhereItWas()
    {
        SplittableBlock story = new SplittableBlock(unitCount: 8, unitHeight: 30);

        LayoutHarness.Measure(Columns(story, balanced: true), new Extent(100, 90));

        Assert.Equal(8, story.Remaining);
    }

    [Fact]
    public void BalancedColumnsEndLevel()
    {
        FlowColumnsBlock columns = Columns(new SplittableBlock(unitCount: 4, unitHeight: 30), balanced: true);
        Extent space = new Extent(100, 150);

        Fit plan = LayoutHarness.Measure(columns, space);
        List<Offset> units = Units(LayoutHarness.Draw(columns, space));

        Assert.Equal(60f, plan.Size.Height, 1);
        Assert.Equal([new Offset(0, 0), new Offset(0, 30), new Offset(55, 0), new Offset(55, 30)], units);
    }

    [Fact]
    public void BalancingWaitsForTheLastPage()
    {
        FlowColumnsBlock columns = Columns(new SplittableBlock(unitCount: 8, unitHeight: 30), balanced: true);

        Assert.Equal(90f, LayoutHarness.Measure(columns, new Extent(100, 90)).Size.Height);
    }

    [Fact]
    public void AShortStoryIsSpreadAcrossTheColumns()
    {
        FlowColumnsBlock columns = Columns(new SplittableBlock(unitCount: 2, unitHeight: 30), balanced: true);

        Assert.Equal(30f, LayoutHarness.Measure(columns, new Extent(100, 150)).Size.Height, 1);
    }

    [Fact]
    public void OneColumnHasNothingToBalance()
    {
        FlowColumnsBlock columns = new FlowColumnsBlock { Count = 1, Balanced = true, Story = new SplittableBlock(unitCount: 2, unitHeight: 30) };

        Assert.Equal(60f, LayoutHarness.Measure(columns, new Extent(100, 150)).Size.Height);
    }

    [Fact]
    public void WhatIsBetweenIsDrawnInEachGutterInUse()
    {
        FlowColumnsBlock columns = new FlowColumnsBlock
        {
            Count = 3,
            Gutter = 5,
            Story = new SplittableBlock(unitCount: 4, unitHeight: 30),
            Between = new VerticalRuleBlock { Weight = 1, Ink = TestInks.Red },
        };

        List<RectangleOperation> rules = LayoutHarness.Draw(columns, new Extent(100, 60)).Operations.OfType<RectangleOperation>()
            .Where(operation => operation.Ink == TestInks.Red)
            .ToList();

        // Two columns of three are used, so one gutter, at the end of the first column, as tall as the columns.
        RectangleOperation rule = Assert.Single(rules);
        Approximately.Equal(new Offset(30, 0), rule.Position);
        Approximately.Equal(new Extent(1, 60), rule.Size);
    }

    [Fact]
    public void RightToLeftTheFirstColumnIsOnTheRight()
    {
        FlowColumnsBlock columns = Columns(new SplittableBlock(unitCount: 4, unitHeight: 30), between: new VerticalRuleBlock { Weight = 1, Ink = TestInks.Red });
        PlanContext context = LayoutHarness.Context();
        context.ReadingDirection = ReadingDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(columns, new Extent(100, 60), context);

        Assert.Equal([new Offset(55, 0), new Offset(55, 30), new Offset(0, 0), new Offset(0, 30)], Units(page));
        Approximately.Equal(new Offset(45, 0), page.Operations.OfType<RectangleOperation>().Single(operation => operation.Ink == TestInks.Red).Position);
    }

    [Fact]
    public void AStoryThatFitsNoColumnMovesOn()
    {
        FlowColumnsBlock columns = Columns(new SplittableBlock(unitCount: 2, unitHeight: 30));

        Assert.True(LayoutHarness.Measure(columns, new Extent(100, 20)).IsDeferred);
        Assert.Empty(LayoutHarness.Draw(columns, new Extent(100, 20)).Operations);
    }

    [Fact]
    public void ColumnsWithNoWidthMoveOn()
    {
        FlowColumnsBlock columns = new FlowColumnsBlock { Count = 3, Gutter = 60, Story = new SplittableBlock(unitCount: 2, unitHeight: 30) };

        Assert.True(LayoutHarness.Measure(columns, new Extent(100, 90)).IsDeferred);
        Assert.Empty(LayoutHarness.Draw(columns, new Extent(100, 90)).Operations);
    }

    [Fact]
    public void ColumnsWithoutAStoryAreEmpty()
    {
        Assert.Equal(Extent.Zero, LayoutHarness.Measure(new FlowColumnsBlock(), new Extent(100, 90)).Size);
        Assert.Empty(LayoutHarness.Draw(new FlowColumnsBlock(), new Extent(100, 90)).Operations);
    }

    [Fact]
    public void TheComposerSetsTheColumns()
    {
        Block root = LayoutHarness.Build(frame => frame.FlowColumns(columns =>
        {
            columns.Columns(3);
            columns.Gutter(12);
            columns.Balanced();
            columns.Story().Text("Story");
            columns.Between().VerticalRule();
        }));

        FlowColumnsBlock block = Assert.IsType<FlowColumnsBlock>(Assert.IsAssignableFrom<Layout.EnclosingBlock>(root).Child);
        Assert.Equal((3, 12f, true), (block.Count, block.Gutter, block.Balanced));
        Assert.NotNull(block.Story);
        Assert.NotNull(block.Between);
    }

    [Fact]
    public void AStoryFlowsThroughAtLeastOneColumn()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LayoutHarness.Build(frame => frame.FlowColumns(columns => columns.Columns(0))));
        Assert.Throws<ArgumentNullException>(() => LayoutHarness.Build(frame => frame.FlowColumns(null!)));
    }

    [Fact]
    public void ADocumentFlowsItsStoryThroughTheColumnsOfEveryPage()
    {
        List<RecordedPage> pages = LayoutHarness.Render(Document.Compose(container => container.Section(section =>
        {
            section.Trim = new Extent(100, 90);
            section.Body().FlowColumns(columns =>
            {
                columns.Gutter(10);
                columns.Story().Compose(frame => frame.Slot().Child = new SplittableBlock(unitCount: 14, unitHeight: 30));
            });
        }))).Pages;

        Assert.Equal(3, pages.Count);
        Assert.Equal([6, 6, 2], pages.Select(page => Units(page).Count));
    }

    [Fact]
    public void AnAnchorInTheStoryIsWhereTheStoryIsDrawnNotWhereItWasTried()
    {
        // The story is poured on the first page to see whether it fits there, and it does not, so it is kept for the
        // second. The anchor inside it is on the second page, however many pages it was tried on.
        List<RecordedPage> pages = LayoutHarness.Render(Document.Compose(container => container.Section(section =>
        {
            section.Trim = new Extent(100, 102);
            section.RunningFoot().Text(text => text.FolioOf("story"));
            section.Body().Stack(stack =>
            {
                stack.Add().Compose(frame => frame.Slot().Child = new FixedBlock(10, 60));
                stack.Add().KeepTogether().FlowColumns(columns =>
                {
                    columns.Gutter(10);
                    columns.Story().Anchor("story").Compose(frame => frame.Slot().Child = new SplittableBlock(unitCount: 4, unitHeight: 30));
                });
            });
        }))).Pages;

        Assert.Equal(2, pages.Count);
        Assert.Equal(["2", "2"], pages.Select(page => page.Content));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AStoryIsCapturedWhereItIsDrawnNotWhereItWasTried(bool balanced)
    {
        CaptureBlock story = new CaptureBlock { Name = "story", Child = new SplittableBlock(unitCount: 4, unitHeight: 30) };
        FlowColumnsBlock columns = new FlowColumnsBlock { Count = 2, Gutter = 10, Balanced = balanced, Story = story };
        PlanContext context = LayoutHarness.Context();

        LayoutHarness.Draw(columns, new Extent(100, 90), context);

        // Once for each column the story is drawn in, and none for the pours that only tried it.
        Assert.Equal([new Offset(0, 0), new Offset(55, 0)], context.Pagination.PositionsOf("story").Select(captured => captured.Position));
    }
}
