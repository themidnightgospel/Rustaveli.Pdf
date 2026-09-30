namespace Rustaveli.Pdf.UnitTests;

public class ReadingDirectionTests
{
    private static ColumnsBlock TwoColumnRow()
    {
        ColumnsBlock row = new ColumnsBlock();

        row.Items.Add(new ColumnSlot { Sizing = ColumnSizing.Fixed, Value = 50, Child = new FixedBlock(1, 10, TestInks.Red) });
        row.Items.Add(new ColumnSlot { Sizing = ColumnSizing.Fixed, Value = 50, Child = new FixedBlock(1, 10, TestInks.Blue) });

        return row;
    }

    [Fact]
    public void RowsInheritDirectionFromTheContext()
    {
        PlanContext context = LayoutHarness.Context();
        context.ReadingDirection = ReadingDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Render(TwoColumnRow(), new Extent(200, 100), context);
        RectangleOperation first = page.Operations.OfType<RectangleOperation>().Single(r => r.Ink == TestInks.Red);

        Approximately.Equal(150f, first.Position.X);
    }

    [Fact]
    public void AnExplicitRowDirectionOverridesTheContext()
    {
        ColumnsBlock row = TwoColumnRow();
        row.ReadingDirection = ReadingDirection.LeftToRight;

        PlanContext context = LayoutHarness.Context();
        context.ReadingDirection = ReadingDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Render(row, new Extent(200, 100), context);
        RectangleOperation first = page.Operations.OfType<RectangleOperation>().Single(r => r.Ink == TestInks.Red);

        Approximately.Equal(0f, first.Position.X);
    }

    [Fact]
    public void TextAlignsToTheTrailingEdgeWhenRightToLeft()
    {
        TextBlock block = new TextBlock();
        new TextComposer(block).Run("Hello");

        PlanContext context = LayoutHarness.Context();
        context.ReadingDirection = ReadingDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Render(block, new Extent(100, 100), context);
        TextOperation text = Assert.Single(page.Texts);

        // Five characters at 6pt each leaves 70pt of slack, taken up on the left.
        Approximately.Equal(70f, text.Position.X);
    }

    [Fact]
    public void ExplicitTextAlignmentBeatsTheDirection()
    {
        TextBlock block = new TextBlock();
        new TextComposer(block).Run("Hello");
        block.Alignment = LineAlignment.Left;

        PlanContext context = LayoutHarness.Context();
        context.ReadingDirection = ReadingDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Render(block, new Extent(100, 100), context);

        Approximately.Equal(0f, Assert.Single(page.Texts).Position.X);
    }

    [Fact]
    public void ReadingDirectionBlockScopesTheChangeToItsSubtree()
    {
        Block root = LayoutHarness.Build(frame => frame.RightToLeft().Compose(inner =>
            inner.Slot().Child = TwoColumnRow()));

        RecordedPage page = LayoutHarness.Render(root, new Extent(200, 100));
        RectangleOperation first = page.Operations.OfType<RectangleOperation>().Single(r => r.Ink == TestInks.Red);

        Approximately.Equal(150f, first.Position.X);
    }

    [Fact]
    public void DirectionIsRestoredAfterTheSubtree()
    {
        PlanContext context = LayoutHarness.Context();
        Block root = LayoutHarness.Build(frame => frame.RightToLeft().Compose(inner =>
            inner.Slot().Child = TwoColumnRow()));

        LayoutHarness.Render(root, new Extent(200, 100), context);

        Assert.Equal(ReadingDirection.LeftToRight, context.ReadingDirection);
    }

    [Theory]
    [InlineData(ReadingDirection.LeftToRight, 50f)]
    [InlineData(ReadingDirection.RightToLeft, 30f)]
    public void ReadingDirectionBlockAppliesWhilePlanningToo(ReadingDirection direction, float expectedWidth)
    {
        // Only text flush against the edge lines start from takes a first-line indent, and right-to-left lines
        // start from the right, so the same flush-left paragraph measures 20pt narrower once the direction reaches it.
        PlanContext context = LayoutHarness.Context();
        Block root = LayoutHarness.Build(frame => frame.Reading(direction).Text(text =>
        {
            text.FirstLineIndent(20);
            text.FlushLeft();
            text.Run("Hello");
        }));

        Fit plan = LayoutHarness.Plan(root, new Extent(200, 100), context);

        Approximately.Equal(expectedWidth, plan.Size.Width);
        Assert.Equal(ReadingDirection.LeftToRight, context.ReadingDirection);
    }

    [Fact]
    public void TablesMirrorTheirColumnsWhenRightToLeft()
    {
        TableBlock block = new TableBlock();
        TableComposer composer = new TableComposer(block);

        composer.Columns(columns =>
        {
            columns.Fixed(60);
            columns.Fixed(60);
        });

        composer.Cell().Compose(frame => frame.Slot().Child = new FixedBlock(1, 10, TestInks.Red));
        composer.Cell().Compose(frame => frame.Slot().Child = new FixedBlock(1, 10, TestInks.Blue));
        composer.PlaceAutomaticCells();

        block.ReadingDirection = ReadingDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Render(block, new Extent(120, 100));
        RectangleOperation first = page.Operations.OfType<RectangleOperation>().Single(r => r.Ink == TestInks.Red);

        Approximately.Equal(60f, first.Position.X);
    }
}
