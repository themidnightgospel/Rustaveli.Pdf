namespace Rustaveli.Pdf.UnitTests;

public class ColumnsTests
{
    private static ColumnsBlock Row(float spacing, params ColumnSlot[] items)
    {
        ColumnsBlock row = new ColumnsBlock { Gutter = spacing };
        row.Items.AddRange(items);
        return row;
    }

    private static ColumnSlot Item(ColumnSizing sizing, float value, Block child) =>
        new() { Sizing = sizing, Value = value, Child = child };

    [Fact]
    public void SplitsWidthBetweenRelativeItemsByWeight()
    {
        ColumnsBlock row = Row(0,
            Item(ColumnSizing.Share, 1, new FixedBlock(1, 10)),
            Item(ColumnSizing.Share, 3, new FixedBlock(1, 10)));

        RecordedPage page = LayoutHarness.Draw(row, new Extent(200, 100));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        // Weights 1:3 across 200pt place the second item at 50.
        Approximately.Equal(0f, rectangles[0].Position.X);
        Approximately.Equal(50f, rectangles[1].Position.X);
    }

    [Fact]
    public void GivesConstantItemsTheirExactWidth()
    {
        ColumnsBlock row = Row(0,
            Item(ColumnSizing.Fixed, 60, new FixedBlock(1, 10)),
            Item(ColumnSizing.Share, 1, new FixedBlock(1, 10)));

        RecordedPage page = LayoutHarness.Draw(row, new Extent(200, 100));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(60f, rectangles[1].Position.X);
    }

    [Fact]
    public void SizesAutoItemsToTheirContent()
    {
        ColumnsBlock row = Row(0,
            Item(ColumnSizing.Natural, 0, new FixedBlock(35, 10)),
            Item(ColumnSizing.Share, 1, new FixedBlock(1, 10)));

        RecordedPage page = LayoutHarness.Draw(row, new Extent(200, 100));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(35f, rectangles[1].Position.X);
    }

    [Fact]
    public void SubtractsSpacingBeforeDistributingWidth()
    {
        ColumnsBlock row = Row(20,
            Item(ColumnSizing.Share, 1, new FixedBlock(1, 10)),
            Item(ColumnSizing.Share, 1, new FixedBlock(1, 10)));

        RecordedPage page = LayoutHarness.Draw(row, new Extent(200, 100));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        // 200 less 20 of spacing leaves 90 each, so the second item starts at 90 + 20.
        Approximately.Equal(110f, rectangles[1].Position.X);
    }

    [Fact]
    public void TakesTheHeightOfItsTallestItem()
    {
        ColumnsBlock row = Row(0,
            Item(ColumnSizing.Share, 1, new FixedBlock(1, 10)),
            Item(ColumnSizing.Share, 1, new FixedBlock(1, 45)));

        Fit plan = LayoutHarness.Measure(row, new Extent(200, 100));

        Approximately.Equal(45f, plan.Size.Height);
        Approximately.Equal(200f, plan.Size.Width);
    }

    [Fact]
    public void PlacesItemsFromTheRightWhenDirectionIsReversed()
    {
        ColumnsBlock row = Row(0,
            Item(ColumnSizing.Fixed, 50, new FixedBlock(1, 10)),
            Item(ColumnSizing.Fixed, 50, new FixedBlock(1, 10)));
        row.ReadingDirection = ReadingDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(row, new Extent(200, 100));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        // The first declared item sits at the right edge.
        Approximately.Equal(150f, rectangles[0].Position.X);
        Approximately.Equal(100f, rectangles[1].Position.X);
    }

    [Fact]
    public void KeepsAutoColumnWidthOnContinuationPages()
    {
        // An Auto item is sized from what its content measures. A stateful child reports only what it has left,
        // so recomputing on page two would collapse the column and shift every column beside it.
        ColumnsBlock row = Row(0,
            Item(ColumnSizing.Natural, 0, new SplittableBlock(unitCount: 4, unitHeight: 30, width: 40)),
            Item(ColumnSizing.Fixed, 30, new FixedBlock(30, 20, TestInks.Red)));

        Extent space = new Extent(200, 60);

        RecordedPage firstPage = LayoutHarness.Draw(row, space);
        RecordedPage secondPage = LayoutHarness.Draw(row, space);

        RectangleOperation firstRed = firstPage.Operations.OfType<RectangleOperation>().Single(r => r.Ink == TestInks.Red);

        // The constant column must stay put; on page two the finished item is not redrawn at all.
        Approximately.Equal(40f, firstRed.Position.X);
        Assert.DoesNotContain(secondPage.Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
    }

    [Fact]
    public void DoesNotRedrawFinishedItemsOnLaterPages()
    {
        // A stateless leaf has no way to report itself finished, so the row has to remember for it.
        ColumnsBlock row = Row(0,
            Item(ColumnSizing.Fixed, 30, new FixedBlock(30, 20, TestInks.Red)),
            Item(ColumnSizing.Share, 1, new SplittableBlock(unitCount: 6, unitHeight: 30)));

        Extent space = new Extent(200, 60);

        RecordedPage firstPage = LayoutHarness.Draw(row, space);
        RecordedPage secondPage = LayoutHarness.Draw(row, space);

        Assert.Contains(firstPage.Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
        Assert.DoesNotContain(secondPage.Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
    }

    [Fact]
    public void ReportsPartialWhenAnyItemHasContentLeft()
    {
        ColumnsBlock row = Row(0,
            Item(ColumnSizing.Share, 1, new FixedBlock(1, 10)),
            Item(ColumnSizing.Share, 1, new SplittableBlock(unitCount: 4, unitHeight: 30)));

        Fit plan = LayoutHarness.Measure(row, new Extent(200, 60));

        Assert.True(plan.IsPartial);
    }

    [Fact]
    public void AnEmptyRowOccupiesNothing()
    {
        ColumnsBlock row = Row(10);

        Fit plan = LayoutHarness.Measure(row, new Extent(200, 100));

        Assert.True(plan.IsComplete);
        Approximately.Equal(Extent.Zero, plan.Size);
        Assert.Empty(LayoutHarness.Draw(row, new Extent(200, 100)).Operations);
    }

    [Theory]
    [InlineData(100f, false)]
    [InlineData(99f, true)]
    public void WrapsWhenItsFixedColumnsAndSpacingCannotFit(float availableWidth, bool wraps)
    {
        // 60 + 30 of constant columns plus one 10pt gap need exactly 100pt.
        ColumnsBlock row = Row(10,
            Item(ColumnSizing.Fixed, 60, new FixedBlock(1, 10)),
            Item(ColumnSizing.Fixed, 30, new FixedBlock(1, 10)));

        Fit plan = LayoutHarness.Measure(row, new Extent(availableWidth, 100));

        Assert.Equal(wraps, plan.IsDeferred);
    }

    [Fact]
    public void TreatsANegativeConstantWidthAsZero()
    {
        ColumnsBlock row = Row(0,
            Item(ColumnSizing.Fixed, -50, new FixedBlock(0, 10, TestInks.Red)),
            Item(ColumnSizing.Share, 1, new FixedBlock(1, 10, TestInks.Blue)));

        RecordedPage page = LayoutHarness.Draw(row, new Extent(20, 100));
        RectangleOperation relative = page.Operations.OfType<RectangleOperation>().Single(r => r.Ink == TestInks.Blue);

        Approximately.Equal(0f, relative.Position.X);
    }

    [Fact]
    public void DrawsEveryItemAtTheRowsHeight()
    {
        // Cell backgrounds and borders only line up if a short item is given the tall item's height.
        ScriptedBlock shortItem = new ScriptedBlock(Fit.Complete(10, 20));
        ScriptedBlock tallItem = new ScriptedBlock(Fit.Complete(10, 45));
        ColumnsBlock row = Row(0, Item(ColumnSizing.Share, 1, shortItem), Item(ColumnSizing.Share, 1, tallItem));

        // Offered exactly the row's own height, so the answer does not depend on who decides it.
        LayoutHarness.Draw(row, new Extent(200, 45));

        Approximately.Equal(new Extent(100, 45), Assert.Single(shortItem.DrawnWith));
        Approximately.Equal(new Extent(100, 45), Assert.Single(tallItem.DrawnWith));
    }

    [Fact]
    public void ReportsEmptyOnceEveryItemHasFinished()
    {
        ColumnsBlock row = Row(0,
            Item(ColumnSizing.Fixed, 30, new FixedBlock(30, 20)),
            Item(ColumnSizing.Share, 1, new FixedBlock(10, 10)));

        Extent space = new Extent(200, 60);
        LayoutHarness.Draw(row, space);

        Assert.True(LayoutHarness.Measure(row, space).IsNothing);
        Assert.Empty(LayoutHarness.Draw(row, space).Operations);
    }

    [Fact]
    public void AnItemWithNothingLeftIsNotDrawnAndLeavesTheRowToTheOthers()
    {
        ScriptedBlock finished = new ScriptedBlock(Fit.Nothing());
        ColumnsBlock row = Row(0, Item(ColumnSizing.Share, 1, finished), Item(ColumnSizing.Share, 1, new FixedBlock(10, 30, TestInks.Blue)));
        Extent space = new Extent(200, 60);

        Fit plan = LayoutHarness.Measure(row, space);
        RecordedPage page = LayoutHarness.Draw(row, space);

        Assert.True(plan.IsComplete);
        Approximately.Equal(new Extent(200, 30), plan.Size);
        Assert.Empty(finished.DrawnWith);
        Approximately.Equal(new Offset(100, 0), Assert.Single(page.Operations.OfType<RectangleOperation>()).Position);
        Assert.True(LayoutHarness.Measure(row, space).IsNothing);
    }

    [Fact]
    public void AnItemThatCannotFitStopsTheWholeRowWithoutFinishingTheOthers()
    {
        // Nothing is drawn on the cramped page, so nothing may be marked finished either — otherwise the red
        // item would be skipped on the page where the row finally fits.
        ColumnsBlock row = Row(0,
            Item(ColumnSizing.Fixed, 50, new FixedBlock(10, 10, TestInks.Red)),
            Item(ColumnSizing.Share, 1, new FixedBlock(10, 200, TestInks.Blue)));

        RecordedPage cramped = LayoutHarness.Draw(row, new Extent(200, 100));
        RecordedPage roomy = LayoutHarness.Draw(row, new Extent(200, 300));

        Assert.Empty(cramped.Operations);
        Assert.Contains(roomy.Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
        Assert.Contains(roomy.Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Blue);
    }

    [Fact]
    public void AnAutoItemThatCannotFitMakesTheRowWrap()
    {
        ColumnsBlock row = Row(0,
            Item(ColumnSizing.Natural, 0, new FixedBlock(30, 500)),
            Item(ColumnSizing.Share, 1, new FixedBlock(10, 10)));

        Assert.True(LayoutHarness.Measure(row, new Extent(200, 100)).IsDeferred);
    }
}
