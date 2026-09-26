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

        RecordedPage page = LayoutHarness.Draw(TwoColumnRow(), new Extent(200, 100), context);
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

        RecordedPage page = LayoutHarness.Draw(row, new Extent(200, 100), context);
        RectangleOperation first = page.Operations.OfType<RectangleOperation>().Single(r => r.Ink == TestInks.Red);

        Approximately.Equal(0f, first.Position.X);
    }

    [Fact]
    public void TextAlignsToTheTrailingEdgeWhenRightToLeft()
    {
        TextBlock element = new TextBlock();
        new TextComposer(element).Run("Hello");

        PlanContext context = LayoutHarness.Context();
        context.ReadingDirection = ReadingDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(element, new Extent(100, 100), context);
        TextOperation text = Assert.Single(page.Texts);

        // Five characters at 6pt each leaves 70pt of slack, taken up on the left.
        Approximately.Equal(70f, text.Position.X);
    }

    [Fact]
    public void ExplicitTextAlignmentBeatsTheDirection()
    {
        TextBlock element = new TextBlock();
        new TextComposer(element).Run("Hello");
        element.Alignment = HorizontalPlacement.Left;

        PlanContext context = LayoutHarness.Context();
        context.ReadingDirection = ReadingDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(element, new Extent(100, 100), context);

        Approximately.Equal(0f, Assert.Single(page.Texts).Position.X);
    }

    [Fact]
    public void ReadingDirectionBlockScopesTheChangeToItsSubtree()
    {
        Block root = LayoutHarness.Build(container => container.RightToLeft().Compose(inner =>
            inner.Slot().Child = TwoColumnRow()));

        RecordedPage page = LayoutHarness.Draw(root, new Extent(200, 100));
        RectangleOperation first = page.Operations.OfType<RectangleOperation>().Single(r => r.Ink == TestInks.Red);

        Approximately.Equal(150f, first.Position.X);
    }

    [Fact]
    public void DirectionIsRestoredAfterTheSubtree()
    {
        PlanContext context = LayoutHarness.Context();
        Block root = LayoutHarness.Build(container => container.RightToLeft().Compose(inner =>
            inner.Slot().Child = TwoColumnRow()));

        LayoutHarness.Draw(root, new Extent(200, 100), context);

        Assert.Equal(ReadingDirection.LeftToRight, context.ReadingDirection);
    }

    [Theory]
    [InlineData(ReadingDirection.LeftToRight, 50f)]
    [InlineData(ReadingDirection.RightToLeft, 30f)]
    public void ReadingDirectionBlockAppliesWhilePlanningToo(ReadingDirection direction, float expectedWidth)
    {
        // Only left-aligned text takes a first-line indent, and right-to-left text aligns right, so the same
        // paragraph measures 20pt narrower once the direction reaches it.
        PlanContext context = LayoutHarness.Context();
        Block root = LayoutHarness.Build(container => container.Reading(direction).Text(text =>
        {
            text.FirstLineIndent(20);
            text.Run("Hello");
        }));

        Fit plan = LayoutHarness.Measure(root, new Extent(200, 100), context);

        Approximately.Equal(expectedWidth, plan.Size.Width);
        Assert.Equal(ReadingDirection.LeftToRight, context.ReadingDirection);
    }

    [Fact]
    public void TablesMirrorTheirColumnsWhenRightToLeft()
    {
        TableBlock element = new TableBlock();
        TableComposer descriptor = new TableComposer(element);

        descriptor.Columns(columns =>
        {
            columns.Fixed(60);
            columns.Fixed(60);
        });

        descriptor.Cell().Compose(container => container.Slot().Child = new FixedBlock(1, 10, TestInks.Red));
        descriptor.Cell().Compose(container => container.Slot().Child = new FixedBlock(1, 10, TestInks.Blue));
        descriptor.PlaceAutomaticCells();

        element.ReadingDirection = ReadingDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(element, new Extent(120, 100));
        RectangleOperation first = page.Operations.OfType<RectangleOperation>().Single(r => r.Ink == TestInks.Red);

        Approximately.Equal(60f, first.Position.X);
    }
}
