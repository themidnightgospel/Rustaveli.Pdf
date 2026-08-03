namespace Rustaveli.Pdf.UnitTests;

public class RowTests
{
    private static RowElement Row(float spacing, params RowItem[] items)
    {
        RowElement row = new RowElement { Spacing = spacing };
        row.Items.AddRange(items);
        return row;
    }

    private static RowItem Item(RowItemSizing sizing, float value, Element child) =>
        new() { Sizing = sizing, Value = value, Child = child };

    [Fact]
    public void SplitsWidthBetweenRelativeItemsByWeight()
    {
        RowElement row = Row(0,
            Item(RowItemSizing.Relative, 1, new FixedElement(1, 10)),
            Item(RowItemSizing.Relative, 3, new FixedElement(1, 10)));

        RecordedPage page = LayoutHarness.Draw(row, new Size(200, 100));
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

        RecordedPage page = LayoutHarness.Draw(row, new Size(200, 100));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(60f, rectangles[1].Position.X);
    }

    [Fact]
    public void SizesAutoItemsToTheirContent()
    {
        RowElement row = Row(0,
            Item(RowItemSizing.Auto, 0, new FixedElement(35, 10)),
            Item(RowItemSizing.Relative, 1, new FixedElement(1, 10)));

        RecordedPage page = LayoutHarness.Draw(row, new Size(200, 100));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(35f, rectangles[1].Position.X);
    }

    [Fact]
    public void SubtractsSpacingBeforeDistributingWidth()
    {
        RowElement row = Row(20,
            Item(RowItemSizing.Relative, 1, new FixedElement(1, 10)),
            Item(RowItemSizing.Relative, 1, new FixedElement(1, 10)));

        RecordedPage page = LayoutHarness.Draw(row, new Size(200, 100));
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

        SpacePlan plan = LayoutHarness.Measure(row, new Size(200, 100));

        Approximately.Equal(45f, plan.Size.Height);
        Approximately.Equal(200f, plan.Size.Width);
    }

    [Fact]
    public void PlacesItemsFromTheRightWhenDirectionIsReversed()
    {
        RowElement row = Row(0,
            Item(RowItemSizing.Constant, 50, new FixedElement(1, 10)),
            Item(RowItemSizing.Constant, 50, new FixedElement(1, 10)));
        row.Direction = ContentDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(row, new Size(200, 100));
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
            Item(RowItemSizing.Constant, 30, new FixedElement(30, 20, Colors.Red)));

        Size space = new Size(200, 60);

        RecordedPage firstPage = LayoutHarness.Draw(row, space);
        RecordedPage secondPage = LayoutHarness.Draw(row, space);

        RectangleOperation firstRed = firstPage.Operations.OfType<RectangleOperation>().Single(r => r.Color == Colors.Red);

        // The constant column must stay put; on page two the finished item is not redrawn at all.
        Approximately.Equal(40f, firstRed.Position.X);
        Assert.DoesNotContain(secondPage.Operations.OfType<RectangleOperation>(), r => r.Color == Colors.Red);
    }

    [Fact]
    public void DoesNotRedrawFinishedItemsOnLaterPages()
    {
        // A stateless leaf has no way to report itself finished, so the row has to remember for it.
        RowElement row = Row(0,
            Item(RowItemSizing.Constant, 30, new FixedElement(30, 20, Colors.Red)),
            Item(RowItemSizing.Relative, 1, new SplittableElement(unitCount: 6, unitHeight: 30)));

        Size space = new Size(200, 60);

        RecordedPage firstPage = LayoutHarness.Draw(row, space);
        RecordedPage secondPage = LayoutHarness.Draw(row, space);

        Assert.Contains(firstPage.Operations.OfType<RectangleOperation>(), r => r.Color == Colors.Red);
        Assert.DoesNotContain(secondPage.Operations.OfType<RectangleOperation>(), r => r.Color == Colors.Red);
    }

    [Fact]
    public void ReportsPartialRenderWhenAnyItemHasContentLeft()
    {
        RowElement row = Row(0,
            Item(RowItemSizing.Relative, 1, new FixedElement(1, 10)),
            Item(RowItemSizing.Relative, 1, new SplittableElement(unitCount: 4, unitHeight: 30)));

        SpacePlan plan = LayoutHarness.Measure(row, new Size(200, 60));

        Assert.True(plan.IsPartialRender);
    }
}
