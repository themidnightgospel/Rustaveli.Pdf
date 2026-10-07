namespace Rustaveli.Pdf.UnitTests;

public class StackTests
{
    private static StackBlock Column(float spacing, params Block[] items)
    {
        StackBlock column = new StackBlock { SpaceBetween = spacing };
        column.Items.AddRange(items);
        return column;
    }

#if NET
    [Fact]
    public void DrawingAStackAllocatesNothingForTheItemsItPlaces()
    {
        // Allocation budget: a stack records where each item it draws went. Every list, page and cell holds one, and
        // a long report draws thousands; the record is lent from one stack to the next, and once it has grown to the
        // largest stack drawn, drawing allocates nothing for it.
        const long Budget = 0;
        PlanContext context = new PlanContext(new FakeTypeMeasurer(), new Pagination());
        RenderContext drawing = new RenderContext(new NullSurface(), context);
        Extent room = new Extent(200, 500);
        StackBlock warm = Column(4, Enumerable.Range(0, 8).Select(_ => (Block)new FixedBlock(10, 10)).ToArray());
        warm.Plan(room, context);
        warm.Render(room, drawing);
        StackBlock column = Column(4, Enumerable.Range(0, 6).Select(_ => (Block)new FixedBlock(50, 20)).ToArray());
        column.Plan(room, context);

        long before = GC.GetAllocatedBytesForCurrentThread();
        column.Render(room, drawing);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated <= Budget, $"Drawing the stack allocated {allocated} bytes; its budget is {Budget}.");
    }
#endif

    [Fact]
    public void AStackDrawnAfterOneThatFailedDrawsOnlyItsOwnItems()
    {
        // The record of placed items is lent from one stack to the next. One whose drawing throws, with items already
        // in it, must not leave them there for the next.
        StackBlock failing = Column(0, new FixedBlock(10, 10), new FixedBlock(10, 10), new ThrowingBlock(new InvalidOperationException("Drawing failed.")));
        Assert.Throws<InvalidOperationException>(() => LayoutHarness.Render(failing, new Extent(200, 200)));

        RecordedPage page = LayoutHarness.Render(Column(0, new FixedBlock(30, 15)), new Extent(200, 200));

        RectangleOperation drawn = Assert.Single(page.Operations.OfType<RectangleOperation>());
        Approximately.Equal(0f, drawn.Position.Y);
    }

    [Fact]
    public void SumsItemHeightsAndTakesTheWidestItem()
    {
        StackBlock column = Column(0, new FixedBlock(50, 20), new FixedBlock(80, 30));

        Fit plan = LayoutHarness.Plan(column, new Extent(200, 200));

        Approximately.Equal(new Extent(80, 50), plan.Size);
    }

    [Fact]
    public void InsertsSpacingBetweenItemsButNotAroundThem()
    {
        StackBlock column = Column(10, new FixedBlock(10, 20), new FixedBlock(10, 20), new FixedBlock(10, 20));

        Fit plan = LayoutHarness.Plan(column, new Extent(200, 200));

        // Three 20pt items plus two 10pt gaps.
        Approximately.Equal(80f, plan.Size.Height);
    }

    [Fact]
    public void StacksItemsTopToBottom()
    {
        StackBlock column = Column(5, new FixedBlock(10, 20), new FixedBlock(10, 30));

        RecordedPage page = LayoutHarness.Render(column, new Extent(200, 200));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(0f, rectangles[0].Position.Y);
        Approximately.Equal(25f, rectangles[1].Position.Y);
    }

    [Fact]
    public void ReportsPartialWhenAnItemIsLeftOver()
    {
        StackBlock column = Column(0, new FixedBlock(10, 60), new FixedBlock(10, 60));

        Fit plan = LayoutHarness.Plan(column, new Extent(200, 100));

        Assert.True(plan.IsPartial);
        Approximately.Equal(60f, plan.Size.Height);
    }

    [Fact]
    public void ResumesAfterTheItemsAlreadyDrawn()
    {
        StackBlock column = Column(0, new FixedBlock(10, 60), new FixedBlock(10, 60));
        Extent space = new Extent(200, 100);

        LayoutHarness.Render(column, space);

        // The first item is finished, so a second pass must start at the second one.
        RecordedPage page = LayoutHarness.Render(column, space);
        RectangleOperation rectangle = Assert.Single(page.Operations.OfType<RectangleOperation>());

        Approximately.Equal(0f, rectangle.Position.Y);
        Assert.True(LayoutHarness.Plan(column, space).IsNothing);
    }

    [Fact]
    public void DefersWhenEvenTheFirstItemDoesNotFit()
    {
        StackBlock column = Column(0, new FixedBlock(10, 500));

        Fit plan = LayoutHarness.Plan(column, new Extent(200, 100));

        Assert.True(plan.IsDeferred);
    }

    [Fact]
    public void ReportsEmptyOnceEveryItemIsDrawn()
    {
        StackBlock column = Column(0, new FixedBlock(10, 20));
        Extent space = new Extent(200, 200);

        LayoutHarness.Render(column, space);

        Assert.True(LayoutHarness.Plan(column, space).IsNothing);
    }

    [Fact]
    public void CarriesASplittableItemAcrossTheBoundary()
    {
        SplittableBlock splittable = new SplittableBlock(unitCount: 4, unitHeight: 25);
        StackBlock column = Column(0, splittable);
        Extent space = new Extent(200, 50);

        Fit plan = LayoutHarness.Plan(column, space);
        Assert.True(plan.IsPartial);

        LayoutHarness.Render(column, space);

        Assert.Equal(2, splittable.Remaining);
    }

    [Fact]
    public void ReportsTheFirstItemsOwnReasonWhenItCannotFit()
    {
        FixedBlock item = new FixedBlock(10, 500);
        StackBlock column = Column(0, item);
        Extent space = new Extent(200, 100);

        Assert.Equal(LayoutHarness.Plan(item, space).DeferReason, LayoutHarness.Plan(column, space).DeferReason);
    }

    [Fact]
    public void ReportsEmptyWhenEveryRemainingItemIsExhausted()
    {
        StackBlock column = Column(10, new ScriptedBlock(Fit.Nothing()), new ScriptedBlock(Fit.Nothing()));

        Assert.True(LayoutHarness.Plan(column, new Extent(200, 200)).IsNothing);
    }

    [Fact]
    public void DefersWhenOfferedNegativeHeight()
    {
        // A placeholder would happily claim a negative box; the column must refuse to hand one out.
        StackBlock column = Column(0, new PlaceholderBlock());

        Assert.True(LayoutHarness.Plan(column, new Extent(200, -5)).IsDeferred);
    }

    [Fact]
    public void StopsBeforeAnItemThatTheSpacingWouldPushPastTheBottom()
    {
        StackBlock column = Column(10, new FixedBlock(10, 95), new PlaceholderBlock());

        Fit plan = LayoutHarness.Plan(column, new Extent(200, 100));

        Assert.True(plan.IsPartial);
        Approximately.Equal(new Extent(10, 95), plan.Size);
    }

    [Fact]
    public void DrawingAnExhaustedColumnDoesNotRewindIt()
    {
        StackBlock column = Column(0, new FixedBlock(10, 20));
        Extent space = new Extent(200, 200);

        LayoutHarness.Render(column, space);
        RecordedPage again = LayoutHarness.Render(column, space);

        Assert.Empty(again.Operations);
        Assert.True(LayoutHarness.Plan(column, space).IsNothing);
    }

    [Fact]
    public void AnAttemptWhereTheNextItemCannotFitKeepsTheColumnsPlace()
    {
        StackBlock column = Column(0, new FixedBlock(10, 10, TestInks.Red), new FixedBlock(10, 50, TestInks.Blue));

        LayoutHarness.Render(column, new Extent(200, 20));
        RecordedPage cramped = LayoutHarness.Render(column, new Extent(200, 5));
        RecordedPage roomy = LayoutHarness.Render(column, new Extent(200, 100));

        Assert.Empty(cramped.Operations);

        RectangleOperation resumed = Assert.Single(roomy.Operations.OfType<RectangleOperation>());
        Assert.Equal(TestInks.Blue, resumed.Ink);
        Approximately.Equal(0f, resumed.Position.Y);
    }
}
