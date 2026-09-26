namespace Rustaveli.Pdf.UnitTests;

public class ColumnTests
{
    private static StackBlock Column(float spacing, params Block[] items)
    {
        StackBlock column = new StackBlock { Spacing = spacing };
        column.Items.AddRange(items);
        return column;
    }

    [Fact]
    public void SumsItemHeightsAndTakesTheWidestItem()
    {
        StackBlock column = Column(0, new FixedElement(50, 20), new FixedElement(80, 30));

        Fit plan = LayoutHarness.Measure(column, new Extent(200, 200));

        Approximately.Equal(new Extent(80, 50), plan.Size);
    }

    [Fact]
    public void InsertsSpacingBetweenItemsButNotAroundThem()
    {
        StackBlock column = Column(10, new FixedElement(10, 20), new FixedElement(10, 20), new FixedElement(10, 20));

        Fit plan = LayoutHarness.Measure(column, new Extent(200, 200));

        // Three 20pt items plus two 10pt gaps.
        Approximately.Equal(80f, plan.Size.Height);
    }

    [Fact]
    public void StacksItemsTopToBottom()
    {
        StackBlock column = Column(5, new FixedElement(10, 20), new FixedElement(10, 30));

        RecordedPage page = LayoutHarness.Draw(column, new Extent(200, 200));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(0f, rectangles[0].Position.Y);
        Approximately.Equal(25f, rectangles[1].Position.Y);
    }

    [Fact]
    public void ReportsPartialRenderWhenAnItemIsLeftOver()
    {
        StackBlock column = Column(0, new FixedElement(10, 60), new FixedElement(10, 60));

        Fit plan = LayoutHarness.Measure(column, new Extent(200, 100));

        Assert.True(plan.IsPartialRender);
        Approximately.Equal(60f, plan.Size.Height);
    }

    [Fact]
    public void ResumesAfterTheItemsAlreadyDrawn()
    {
        StackBlock column = Column(0, new FixedElement(10, 60), new FixedElement(10, 60));
        Extent space = new Extent(200, 100);

        LayoutHarness.Draw(column, space);

        // The first item is finished, so a second pass must start at the second one.
        RecordedPage page = LayoutHarness.Draw(column, space);
        RectangleOperation rectangle = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(0f, rectangle.Position.Y);
        Assert.True(LayoutHarness.Measure(column, space).IsEmpty);
    }

    [Fact]
    public void WrapsWhenEvenTheFirstItemDoesNotFit()
    {
        StackBlock column = Column(0, new FixedElement(10, 500));

        Fit plan = LayoutHarness.Measure(column, new Extent(200, 100));

        Assert.True(plan.IsWrap);
    }

    [Fact]
    public void ReportsEmptyOnceEveryItemIsDrawn()
    {
        StackBlock column = Column(0, new FixedElement(10, 20));
        Extent space = new Extent(200, 200);

        LayoutHarness.Draw(column, space);

        Assert.True(LayoutHarness.Measure(column, space).IsEmpty);
    }

    [Fact]
    public void CarriesASplittableItemAcrossTheBoundary()
    {
        SplittableElement splittable = new SplittableElement(unitCount: 4, unitHeight: 25);
        StackBlock column = Column(0, splittable);
        Extent space = new Extent(200, 50);

        Fit plan = LayoutHarness.Measure(column, space);
        Assert.True(plan.IsPartialRender);

        LayoutHarness.Draw(column, space);

        Assert.Equal(2, splittable.Remaining);
    }

    [Fact]
    public void ReportsTheFirstItemsOwnReasonWhenItCannotFit()
    {
        FixedElement item = new FixedElement(10, 500);
        StackBlock column = Column(0, item);
        Extent space = new Extent(200, 100);

        Assert.Equal(LayoutHarness.Measure(item, space).WrapReason, LayoutHarness.Measure(column, space).WrapReason);
    }

    [Fact]
    public void ReportsEmptyWhenEveryRemainingItemIsExhausted()
    {
        StackBlock column = Column(10, new ScriptedElement(Fit.Empty()), new ScriptedElement(Fit.Empty()));

        Assert.True(LayoutHarness.Measure(column, new Extent(200, 200)).IsEmpty);
    }

    [Fact]
    public void WrapsWhenOfferedNegativeHeight()
    {
        // A placeholder would happily claim a negative box; the column must refuse to hand one out.
        StackBlock column = Column(0, new PlaceholderBlock());

        Assert.True(LayoutHarness.Measure(column, new Extent(200, -5)).IsWrap);
    }

    [Fact]
    public void StopsBeforeAnItemThatTheSpacingWouldPushPastTheBottom()
    {
        StackBlock column = Column(10, new FixedElement(10, 95), new PlaceholderBlock());

        Fit plan = LayoutHarness.Measure(column, new Extent(200, 100));

        Assert.True(plan.IsPartialRender);
        Approximately.Equal(new Extent(10, 95), plan.Size);
    }

    [Fact]
    public void DrawingAnExhaustedColumnDoesNotRewindIt()
    {
        StackBlock column = Column(0, new FixedElement(10, 20));
        Extent space = new Extent(200, 200);

        LayoutHarness.Draw(column, space);
        RecordedPage again = LayoutHarness.Draw(column, space);

        Assert.Empty(again.Operations);
        Assert.True(LayoutHarness.Measure(column, space).IsEmpty);
    }

    [Fact]
    public void AnAttemptWhereTheNextItemCannotFitKeepsTheColumnsPlace()
    {
        StackBlock column = Column(0, new FixedElement(10, 10, TestInks.Red), new FixedElement(10, 50, TestInks.Blue));

        LayoutHarness.Draw(column, new Extent(200, 20));
        RecordedPage cramped = LayoutHarness.Draw(column, new Extent(200, 5));
        RecordedPage roomy = LayoutHarness.Draw(column, new Extent(200, 100));

        Assert.Empty(cramped.Operations);

        RectangleOperation resumed = Assert.Single(roomy.Operations.OfType<RectangleOperation>());
        Assert.Equal(TestInks.Blue, resumed.Color);
        Approximately.Equal(0f, resumed.Position.Y);
    }
}
