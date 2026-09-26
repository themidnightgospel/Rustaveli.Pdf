namespace Rustaveli.Pdf.UnitTests;

public class ContentDirectionTests
{
    private static ColumnsBlock TwoColumnRow()
    {
        ColumnsBlock row = new ColumnsBlock();

        row.Items.Add(new ColumnSlot { Sizing = ColumnSizing.Constant, Value = 50, Child = new FixedElement(1, 10, TestInks.Red) });
        row.Items.Add(new ColumnSlot { Sizing = ColumnSizing.Constant, Value = 50, Child = new FixedElement(1, 10, TestInks.Blue) });

        return row;
    }

    [Fact]
    public void RowsInheritDirectionFromTheContext()
    {
        PlanContext context = LayoutHarness.Context();
        context.ContentDirection = ReadingDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(TwoColumnRow(), new Extent(200, 100), context);
        RectangleOperation first = page.Operations.OfType<RectangleOperation>().Single(r => r.Color == TestInks.Red);

        Approximately.Equal(150f, first.Position.X);
    }

    [Fact]
    public void AnExplicitRowDirectionOverridesTheContext()
    {
        ColumnsBlock row = TwoColumnRow();
        row.Direction = ReadingDirection.LeftToRight;

        PlanContext context = LayoutHarness.Context();
        context.ContentDirection = ReadingDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(row, new Extent(200, 100), context);
        RectangleOperation first = page.Operations.OfType<RectangleOperation>().Single(r => r.Color == TestInks.Red);

        Approximately.Equal(0f, first.Position.X);
    }

    [Fact]
    public void TextAlignsToTheTrailingEdgeWhenRightToLeft()
    {
        TextBlock element = new TextBlock();
        new TextComposer(element).Span("Hello");

        PlanContext context = LayoutHarness.Context();
        context.ContentDirection = ReadingDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(element, new Extent(100, 100), context);
        TextOperation text = Assert.Single(page.Texts);

        // Five characters at 6pt each leaves 70pt of slack, taken up on the left.
        Approximately.Equal(70f, text.Position.X);
    }

    [Fact]
    public void ExplicitTextAlignmentBeatsTheDirection()
    {
        TextBlock element = new TextBlock();
        new TextComposer(element).Span("Hello");
        element.Alignment = HorizontalPlacement.Left;

        PlanContext context = LayoutHarness.Context();
        context.ContentDirection = ReadingDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(element, new Extent(100, 100), context);

        Approximately.Equal(0f, Assert.Single(page.Texts).Position.X);
    }

    [Fact]
    public void DirectionElementScopesTheChangeToItsSubtree()
    {
        Block root = LayoutHarness.Build(container => container.RightToLeft().Element(inner =>
            inner.Child = TwoColumnRow()));

        RecordedPage page = LayoutHarness.Draw(root, new Extent(200, 100));
        RectangleOperation first = page.Operations.OfType<RectangleOperation>().Single(r => r.Color == TestInks.Red);

        Approximately.Equal(150f, first.Position.X);
    }

    [Fact]
    public void DirectionIsRestoredAfterTheSubtree()
    {
        PlanContext context = LayoutHarness.Context();
        Block root = LayoutHarness.Build(container => container.RightToLeft().Element(inner =>
            inner.Child = TwoColumnRow()));

        LayoutHarness.Draw(root, new Extent(200, 100), context);

        Assert.Equal(ReadingDirection.LeftToRight, context.ContentDirection);
    }

    [Theory]
    [InlineData(ReadingDirection.LeftToRight, 50f)]
    [InlineData(ReadingDirection.RightToLeft, 30f)]
    public void DirectionElementAppliesWhileMeasuringToo(ReadingDirection direction, float expectedWidth)
    {
        // Only left-aligned text takes a first-line indent, and right-to-left text aligns right, so the same
        // paragraph measures 20pt narrower once the direction reaches it.
        PlanContext context = LayoutHarness.Context();
        Block root = LayoutHarness.Build(container => container.Reading(direction).Text(text =>
        {
            text.FirstLineIndent(20);
            text.Span("Hello");
        }));

        Fit plan = LayoutHarness.Measure(root, new Extent(200, 100), context);

        Approximately.Equal(expectedWidth, plan.Size.Width);
        Assert.Equal(ReadingDirection.LeftToRight, context.ContentDirection);
    }

    [Fact]
    public void TablesMirrorTheirColumnsWhenRightToLeft()
    {
        TableElement element = new TableElement();
        TableComposer descriptor = new TableComposer(element);

        descriptor.ColumnsDefinition(columns =>
        {
            columns.ConstantColumn(60);
            columns.ConstantColumn(60);
        });

        descriptor.Cell().Element(container => container.Child = new FixedElement(1, 10, TestInks.Red));
        descriptor.Cell().Element(container => container.Child = new FixedElement(1, 10, TestInks.Blue));
        descriptor.PlaceAutomaticCells();

        element.Direction = ReadingDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(element, new Extent(120, 100));
        RectangleOperation first = page.Operations.OfType<RectangleOperation>().Single(r => r.Color == TestInks.Red);

        Approximately.Equal(60f, first.Position.X);
    }
}
