namespace Rustaveli.Pdf.UnitTests;

public class RowTests
{
    private static RowElement Row(float spacing, params RowItem[] items)
    {
        RowElement row = new RowElement { Spacing = spacing };
        row.Items.AddRange(items);
        return row;
    }

    private static RowItem Item(RowItemSizing sizing, float value, Block child) =>
        new() { Sizing = sizing, Value = value, Child = child };

    [Fact]
    public void SplitsWidthBetweenRelativeItemsByWeight()
    {
        RowElement row = Row(0,
            Item(RowItemSizing.Relative, 1, new FixedElement(1, 10)),
            Item(RowItemSizing.Relative, 3, new FixedElement(1, 10)));

        RecordedPage page = LayoutHarness.Draw(row, new Extent(200, 100));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        // Weights 1:3 across 200pt place the second item at 50.
        Approximately.Equal(0f, rectangles[0].Position.X);
        Approximately.Equal(50f, rectangles[1].Position.X);
    }

    [Fact]
    public void GivesConstantItemsTheirExactWidth()
    {
        RowElement row = Row(0,
            Item(RowItemSizing.Constant, 60, new FixedElement(1, 10)),
            Item(RowItemSizing.Relative, 1, new FixedElement(1, 10)));

        RecordedPage page = LayoutHarness.Draw(row, new Extent(200, 100));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(60f, rectangles[1].Position.X);
    }

    [Fact]
    public void SizesAutoItemsToTheirContent()
    {
        RowElement row = Row(0,
            Item(RowItemSizing.Auto, 0, new FixedElement(35, 10)),
            Item(RowItemSizing.Relative, 1, new FixedElement(1, 10)));

        RecordedPage page = LayoutHarness.Draw(row, new Extent(200, 100));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(35f, rectangles[1].Position.X);
    }

    [Fact]
    public void SubtractsSpacingBeforeDistributingWidth()
    {
        RowElement row = Row(20,
            Item(RowItemSizing.Relative, 1, new FixedElement(1, 10)),
            Item(RowItemSizing.Relative, 1, new FixedElement(1, 10)));

        RecordedPage page = LayoutHarness.Draw(row, new Extent(200, 100));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        // 200 less 20 of spacing leaves 90 each, so the second item starts at 90 + 20.
        Approximately.Equal(110f, rectangles[1].Position.X);
    }

    [Fact]
    public void TakesTheHeightOfItsTallestItem()
    {
        RowElement row = Row(0,
            Item(RowItemSizing.Relative, 1, new FixedElement(1, 10)),
            Item(RowItemSizing.Relative, 1, new FixedElement(1, 45)));

        Fit plan = LayoutHarness.Measure(row, new Extent(200, 100));

        Approximately.Equal(45f, plan.Size.Height);
        Approximately.Equal(200f, plan.Size.Width);
    }

    [Fact]
    public void PlacesItemsFromTheRightWhenDirectionIsReversed()
    {
        RowElement row = Row(0,
            Item(RowItemSizing.Constant, 50, new FixedElement(1, 10)),
            Item(RowItemSizing.Constant, 50, new FixedElement(1, 10)));
        row.Direction = ReadingDirection.RightToLeft;

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
        RowElement row = Row(0,
            Item(RowItemSizing.Auto, 0, new SplittableElement(unitCount: 4, unitHeight: 30, width: 40)),
            Item(RowItemSizing.Constant, 30, new FixedElement(30, 20, TestInks.Red)));

        Extent space = new Extent(200, 60);

        RecordedPage firstPage = LayoutHarness.Draw(row, space);
        RecordedPage secondPage = LayoutHarness.Draw(row, space);

        RectangleOperation firstRed = firstPage.Operations.OfType<RectangleOperation>().Single(r => r.Color == TestInks.Red);

        // The constant column must stay put; on page two the finished item is not redrawn at all.
        Approximately.Equal(40f, firstRed.Position.X);
        Assert.DoesNotContain(secondPage.Operations.OfType<RectangleOperation>(), r => r.Color == TestInks.Red);
    }

    [Fact]
    public void DoesNotRedrawFinishedItemsOnLaterPages()
    {
        // A stateless leaf has no way to report itself finished, so the row has to remember for it.
        RowElement row = Row(0,
            Item(RowItemSizing.Constant, 30, new FixedElement(30, 20, TestInks.Red)),
            Item(RowItemSizing.Relative, 1, new SplittableElement(unitCount: 6, unitHeight: 30)));

        Extent space = new Extent(200, 60);

        RecordedPage firstPage = LayoutHarness.Draw(row, space);
        RecordedPage secondPage = LayoutHarness.Draw(row, space);

        Assert.Contains(firstPage.Operations.OfType<RectangleOperation>(), r => r.Color == TestInks.Red);
        Assert.DoesNotContain(secondPage.Operations.OfType<RectangleOperation>(), r => r.Color == TestInks.Red);
    }

    [Fact]
    public void ReportsPartialRenderWhenAnyItemHasContentLeft()
    {
        RowElement row = Row(0,
            Item(RowItemSizing.Relative, 1, new FixedElement(1, 10)),
            Item(RowItemSizing.Relative, 1, new SplittableElement(unitCount: 4, unitHeight: 30)));

        Fit plan = LayoutHarness.Measure(row, new Extent(200, 60));

        Assert.True(plan.IsPartialRender);
    }

    [Fact]
    public void AnEmptyRowOccupiesNothing()
    {
        RowElement row = Row(10);

        Fit plan = LayoutHarness.Measure(row, new Extent(200, 100));

        Assert.True(plan.IsFullRender);
        Approximately.Equal(Extent.Zero, plan.Size);
        Assert.Empty(LayoutHarness.Draw(row, new Extent(200, 100)).Operations);
    }

    [Theory]
    [InlineData(100f, false)]
    [InlineData(99f, true)]
    public void WrapsWhenItsFixedColumnsAndSpacingCannotFit(float availableWidth, bool wraps)
    {
        // 60 + 30 of constant columns plus one 10pt gap need exactly 100pt.
        RowElement row = Row(10,
            Item(RowItemSizing.Constant, 60, new FixedElement(1, 10)),
            Item(RowItemSizing.Constant, 30, new FixedElement(1, 10)));

        Fit plan = LayoutHarness.Measure(row, new Extent(availableWidth, 100));

        Assert.Equal(wraps, plan.IsWrap);
    }

    [Fact]
    public void TreatsANegativeConstantWidthAsZero()
    {
        RowElement row = Row(0,
            Item(RowItemSizing.Constant, -50, new FixedElement(0, 10, TestInks.Red)),
            Item(RowItemSizing.Relative, 1, new FixedElement(1, 10, TestInks.Blue)));

        RecordedPage page = LayoutHarness.Draw(row, new Extent(20, 100));
        RectangleOperation relative = page.Operations.OfType<RectangleOperation>().Single(r => r.Color == TestInks.Blue);

        Approximately.Equal(0f, relative.Position.X);
    }

    [Fact]
    public void DrawsEveryItemAtTheRowsHeight()
    {
        // Cell backgrounds and borders only line up if a short item is given the tall item's height.
        ScriptedElement shortItem = new ScriptedElement(Fit.FullRender(10, 20));
        ScriptedElement tallItem = new ScriptedElement(Fit.FullRender(10, 45));
        RowElement row = Row(0, Item(RowItemSizing.Relative, 1, shortItem), Item(RowItemSizing.Relative, 1, tallItem));

        // Offered exactly the row's own height, so the answer does not depend on who decides it.
        LayoutHarness.Draw(row, new Extent(200, 45));

        Approximately.Equal(new Extent(100, 45), Assert.Single(shortItem.DrawnWith));
        Approximately.Equal(new Extent(100, 45), Assert.Single(tallItem.DrawnWith));
    }

    [Fact]
    public void ReportsEmptyOnceEveryItemHasFinished()
    {
        RowElement row = Row(0,
            Item(RowItemSizing.Constant, 30, new FixedElement(30, 20)),
            Item(RowItemSizing.Relative, 1, new FixedElement(10, 10)));

        Extent space = new Extent(200, 60);
        LayoutHarness.Draw(row, space);

        Assert.True(LayoutHarness.Measure(row, space).IsEmpty);
        Assert.Empty(LayoutHarness.Draw(row, space).Operations);
    }

    [Fact]
    public void AnItemThatCannotFitStopsTheWholeRowWithoutFinishingTheOthers()
    {
        // Nothing is drawn on the cramped page, so nothing may be marked finished either — otherwise the red
        // item would be skipped on the page where the row finally fits.
        RowElement row = Row(0,
            Item(RowItemSizing.Constant, 50, new FixedElement(10, 10, TestInks.Red)),
            Item(RowItemSizing.Relative, 1, new FixedElement(10, 200, TestInks.Blue)));

        RecordedPage cramped = LayoutHarness.Draw(row, new Extent(200, 100));
        RecordedPage roomy = LayoutHarness.Draw(row, new Extent(200, 300));

        Assert.Empty(cramped.Operations);
        Assert.Contains(roomy.Operations.OfType<RectangleOperation>(), r => r.Color == TestInks.Red);
        Assert.Contains(roomy.Operations.OfType<RectangleOperation>(), r => r.Color == TestInks.Blue);
    }

    [Fact]
    public void AnAutoItemThatCannotFitMakesTheRowWrap()
    {
        RowElement row = Row(0,
            Item(RowItemSizing.Auto, 0, new FixedElement(30, 500)),
            Item(RowItemSizing.Relative, 1, new FixedElement(10, 10)));

        Assert.True(LayoutHarness.Measure(row, new Extent(200, 100)).IsWrap);
    }
}
