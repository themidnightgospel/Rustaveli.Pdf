namespace Rustaveli.Pdf.UnitTests;

public class ContentDirectionTests
{
    private static RowElement TwoColumnRow()
    {
        RowElement row = new RowElement();

        row.Items.Add(new RowItem { Sizing = RowItemSizing.Constant, Value = 50, Child = new FixedElement(1, 10, TestInks.Red) });
        row.Items.Add(new RowItem { Sizing = RowItemSizing.Constant, Value = 50, Child = new FixedElement(1, 10, TestInks.Blue) });

        return row;
    }

    [Fact]
    public void RowsInheritDirectionFromTheContext()
    {
        LayoutContext context = LayoutHarness.Context();
        context.ContentDirection = ContentDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(TwoColumnRow(), new Size(200, 100), context);
        RectangleOperation first = page.Operations.OfType<RectangleOperation>().Single(r => r.Color == TestInks.Red);

        Approximately.Equal(150f, first.Position.X);
    }

    [Fact]
    public void AnExplicitRowDirectionOverridesTheContext()
    {
        RowElement row = TwoColumnRow();
        row.Direction = ContentDirection.LeftToRight;

        LayoutContext context = LayoutHarness.Context();
        context.ContentDirection = ContentDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(row, new Size(200, 100), context);
        RectangleOperation first = page.Operations.OfType<RectangleOperation>().Single(r => r.Color == TestInks.Red);

        Approximately.Equal(0f, first.Position.X);
    }

    [Fact]
    public void TextAlignsToTheTrailingEdgeWhenRightToLeft()
    {
        TextElement element = new TextElement();
        new TextDescriptor(element).Span("Hello");

        LayoutContext context = LayoutHarness.Context();
        context.ContentDirection = ContentDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(element, new Size(100, 100), context);
        TextOperation text = Assert.Single(page.Texts);

        // Five characters at 6pt each leaves 70pt of slack, taken up on the left.
        Approximately.Equal(70f, text.Position.X);
    }

    [Fact]
    public void ExplicitTextAlignmentBeatsTheDirection()
    {
        TextElement element = new TextElement();
        new TextDescriptor(element).Span("Hello");
        element.Alignment = HorizontalAlignment.Left;

        LayoutContext context = LayoutHarness.Context();
        context.ContentDirection = ContentDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(element, new Size(100, 100), context);

        Approximately.Equal(0f, Assert.Single(page.Texts).Position.X);
    }

    [Fact]
    public void DirectionElementScopesTheChangeToItsSubtree()
    {
        Element root = LayoutHarness.Build(container => container.RightToLeft().Element(inner =>
            inner.Child = TwoColumnRow()));

        RecordedPage page = LayoutHarness.Draw(root, new Size(200, 100));
        RectangleOperation first = page.Operations.OfType<RectangleOperation>().Single(r => r.Color == TestInks.Red);

        Approximately.Equal(150f, first.Position.X);
    }

    [Fact]
    public void DirectionIsRestoredAfterTheSubtree()
    {
        LayoutContext context = LayoutHarness.Context();
        Element root = LayoutHarness.Build(container => container.RightToLeft().Element(inner =>
            inner.Child = TwoColumnRow()));

        LayoutHarness.Draw(root, new Size(200, 100), context);

        Assert.Equal(ContentDirection.LeftToRight, context.ContentDirection);
    }

    [Theory]
    [InlineData(ContentDirection.LeftToRight, 50f)]
    [InlineData(ContentDirection.RightToLeft, 30f)]
    public void DirectionElementAppliesWhileMeasuringToo(ContentDirection direction, float expectedWidth)
    {
        // Only left-aligned text takes a first-line indent, and right-to-left text aligns right, so the same
        // paragraph measures 20pt narrower once the direction reaches it.
        LayoutContext context = LayoutHarness.Context();
        Element root = LayoutHarness.Build(container => container.ContentFrom(direction).Text(text =>
        {
            text.FirstLineIndent(20);
            text.Span("Hello");
        }));

        SpacePlan plan = LayoutHarness.Measure(root, new Size(200, 100), context);

        Approximately.Equal(expectedWidth, plan.Size.Width);
        Assert.Equal(ContentDirection.LeftToRight, context.ContentDirection);
    }

    [Fact]
    public void TablesMirrorTheirColumnsWhenRightToLeft()
    {
        TableElement element = new TableElement();
        TableDescriptor descriptor = new TableDescriptor(element);

        descriptor.ColumnsDefinition(columns =>
        {
            columns.ConstantColumn(60);
            columns.ConstantColumn(60);
        });

        descriptor.Cell().Element(container => container.Child = new FixedElement(1, 10, TestInks.Red));
        descriptor.Cell().Element(container => container.Child = new FixedElement(1, 10, TestInks.Blue));
        descriptor.PlaceAutomaticCells();

        element.Direction = ContentDirection.RightToLeft;

        RecordedPage page = LayoutHarness.Draw(element, new Size(120, 100));
        RectangleOperation first = page.Operations.OfType<RectangleOperation>().Single(r => r.Color == TestInks.Red);

        Approximately.Equal(60f, first.Position.X);
    }
}
