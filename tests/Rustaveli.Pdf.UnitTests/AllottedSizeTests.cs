namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// A parent decides each child's final size and draws it at exactly that size; decorators fill what they are
/// given rather than their content's natural size (docs/adr/0012-parents-allot-final-size.md).
/// </summary>
public class AllottedSizeTests
{
    private static readonly Ink Marker = TestInks.Red;

    private static void Box(IFrame container, float width, float height, Ink? color = null) =>
        FrameAttachment.Attach(container, color is null ? new FixedElement(width, height) : new FixedElement(width, height, color.Value));

    private static RectangleOperation MarkerRectangle(RecordedPage page) =>
        page.Operations.OfType<RectangleOperation>().Single(operation => operation.Color == Marker);

    [Fact]
    public void ABackgroundInAColumnItemSpansTheColumnWidthAtTheItemsHeight()
    {
        RecordedPage page = LayoutHarness.Draw(
            container => container.Column(column => column.Item().Fill(Marker).Element(item => Box(item, 50, 20))),
            new Extent(200, 300));

        RectangleOperation background = MarkerRectangle(page);
        Approximately.Equal(Offset.Zero, background.Position);
        Approximately.Equal(new Extent(200, 20), background.Size);
    }

    [Fact]
    public void ABackgroundInATableCellFillsTheCellIncludingTheRowHeightSetByItsNeighbour()
    {
        RecordedPage page = LayoutHarness.Draw(
            container => container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                table.Cell().Fill(Marker).Element(cell => Box(cell, 10, 10));
                table.Cell().Element(cell => Box(cell, 10, 40));
            }),
            new Extent(200, 300));

        Approximately.Equal(new Extent(100, 40), MarkerRectangle(page).Size);
    }

    [Fact]
    public void ABorderOnARowItemSurroundsTheWholeItemNotItsContent()
    {
        RecordedPage page = LayoutHarness.Draw(
            container => container.Row(row =>
            {
                row.RelativeItem().Stroke(1).StrokeInk(Marker).Element(item => Box(item, 10, 10));
                row.ConstantItem(50).Element(item => Box(item, 50, 30));
            }),
            new Extent(200, 300));

        List<RectangleOperation> bands = page.Operations.OfType<RectangleOperation>().Where(operation => operation.Color == Marker).ToList();
        RectangleOperation right = bands.Single(band => band.Position.X > 0 && band.Size.Width < 2);
        RectangleOperation bottom = bands.Single(band => band.Position.Y > 0 && band.Size.Height < 2);

        Approximately.Equal(149f, right.Position.X);
        Approximately.Equal(30f, right.Size.Height);
        Approximately.Equal(29f, bottom.Position.Y);
        Approximately.Equal(150f, bottom.Size.Width);
    }

    [Fact]
    public void EveryLayerIsGivenTheWholeBoxSoASecondaryLayerCanAlignToItsFarCorner()
    {
        RecordedPage page = LayoutHarness.Draw(
            container => container.Height(100).Layers(layers =>
            {
                layers.PrimaryLayer().Element(layer => Box(layer, 50, 20));
                layers.Layer().FlushRight().FlushBottom().Element(layer => Box(layer, 10, 10, Marker));
            }),
            new Extent(200, 300));

        Approximately.Equal(new Offset(190, 90), MarkerRectangle(page).Position);
    }

    [Fact]
    public void DecorationBandsSpanTheWidthAndTheTrailingBandFollowsTheContent()
    {
        RecordedPage page = LayoutHarness.Draw(
            container => container.Column(column => column.Item().Decoration(decoration =>
            {
                decoration.Before().Fill(Marker).Element(band => Box(band, 10, 10));
                decoration.Content().Element(content => Box(content, 10, 25));
                decoration.After().Fill(TestInks.Blue).Element(band => Box(band, 10, 10));
            })),
            new Extent(200, 300));

        RectangleOperation before = MarkerRectangle(page);
        RectangleOperation after = page.Operations.OfType<RectangleOperation>().Single(operation => operation.Color == TestInks.Blue);

        Approximately.Equal(new Extent(200, 10), before.Size);
        Approximately.Equal(new Extent(200, 10), after.Size);
        Approximately.Equal(35f, after.Position.Y);
    }

    [Fact]
    public void AlignedRightToLeftTextIsCentredOnceRatherThanOffsetTwice()
    {
        // Right-to-left text aligns itself to the right of whatever box it is drawn in. Drawn in the full width
        // after the alignment had already moved it, it was pushed off the far edge of the page.
        RecordedPage page = LayoutHarness.Draw(container => container.RightToLeft().Centered().Text("Hello"), new Extent(100, 100));

        TextOperation text = page.Texts.Single();
        float width = LayoutHarness.Measurer.MeasureWidth("Hello", TypeStyle.Default);

        Approximately.Equal((100 - width) / 2, text.Position.X);
    }

    [Fact]
    public void AQuarterTurnFillsTheBoxItWasGiven()
    {
        RecordedPage page = LayoutHarness.Draw(
            container => container.Width(80).Height(40).TurnLeft().Fill(Marker).Element(inner => Box(inner, 10, 10)),
            new Extent(200, 300));

        Bounds bounds = MarkerRectangle(page).Bounds;
        Approximately.Equal(0f, bounds.Left);
        Approximately.Equal(0f, bounds.Top);
        Approximately.Equal(80f, bounds.Right);
        Approximately.Equal(40f, bounds.Bottom);
    }

    [Fact]
    public void AFlipMirrorsContentAcrossTheBoxItWasGiven()
    {
        RecordedPage page = LayoutHarness.Draw(
            container => container.Width(100).Height(20).MirrorHorizontal().Element(inner => Box(inner, 10, 10, Marker)),
            new Extent(200, 300));

        Bounds bounds = MarkerRectangle(page).Bounds;
        Approximately.Equal(90f, bounds.Left);
        Approximately.Equal(100f, bounds.Right);
    }

    [Fact]
    public void AHyperlinkCoversTheBoxItWasGiven()
    {
        RecordedPage page = LayoutHarness.Draw(
            container => container.Column(column => column.Item().Link("https://example.com").Element(item => Box(item, 10, 10))),
            new Extent(200, 300));

        Approximately.Equal(new Extent(200, 10), page.Operations.OfType<ExternalLinkOperation>().Single().Size);
    }

    [Fact]
    public void AnItemOfNoHeightAfterTheLastVisibleItemIsStillDrawnOnThatPage()
    {
        // Drawn at its final height, a column has no room left for the gap before a trailing item. An item that
        // occupies no height needs no gap, and must still be drawn: its side effects — here a destination, and
        // for markers such as "skip once" a change of state — belong to the page it was measured on.
        RecordedPage page = LayoutHarness.Draw(
            container => container.Column(outer => outer.Item().Column(inner =>
            {
                inner.Spacing(10);
                inner.Item().Element(item => Box(item, 50, 20));
                inner.Item().Anchor("end");
            })),
            new Extent(200, 300));

        Assert.Single(page.Operations.OfType<DestinationOperation>(), operation => operation.Name == "end");
    }
}
