namespace Rustaveli.Pdf.UnitTests;

public class TableTests
{
    private static TableBlock BuildTable(Action<TableComposer> compose)
    {
        TableBlock element = new TableBlock();
        TableComposer descriptor = new TableComposer(element);
        compose(descriptor);
        descriptor.PlaceAutomaticCells();
        return element;
    }

    private static void Fill(IFrame container, float width, float height) =>
        container.Compose(inner => inner.Slot().Child = new FixedBlock(width, height));

    private static void Fill(IFrame container, float width, float height, Ink color) =>
        container.Compose(inner => inner.Slot().Child = new FixedBlock(width, height, color));

    [Fact]
    public void SplitsWidthEvenlyBetweenEqualRelativeColumns()
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns =>
            {
                columns.Share();
                columns.Share();
            });

            Fill(descriptor.Cell(), 1, 10);
            Fill(descriptor.Cell(), 1, 10);
        });

        RecordedPage page = LayoutHarness.Draw(table, new Extent(200, 200));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(0f, rectangles[0].Position.X);
        Approximately.Equal(100f, rectangles[1].Position.X);
    }

    [Fact]
    public void HonoursWeightsBetweenRelativeColumns()
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns =>
            {
                columns.Share(1);
                columns.Share(4);
            });

            Fill(descriptor.Cell(), 1, 10);
            Fill(descriptor.Cell(), 1, 10);
        });

        RecordedPage page = LayoutHarness.Draw(table, new Extent(200, 200));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(40f, rectangles[1].Position.X);
    }

    [Fact]
    public void GivesConstantColumnsTheirExactWidthBeforeSharingTheRest()
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns =>
            {
                columns.Fixed(30);
                columns.Share();
            });

            Fill(descriptor.Cell(), 1, 10);
            Fill(descriptor.Cell(), 1, 10);
        });

        RecordedPage page = LayoutHarness.Draw(table, new Extent(200, 200));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(30f, rectangles[1].Position.X);
    }

    [Fact]
    public void FillsCellsLeftToRightThenWraps()
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns =>
            {
                columns.Share();
                columns.Share();
            });

            for (int index = 0; index < 4; index++)
                Fill(descriptor.Cell(), 1, 20);
        });

        RecordedPage page = LayoutHarness.Draw(table, new Extent(200, 200));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(new Offset(0, 0), rectangles[0].Position);
        Approximately.Equal(new Offset(100, 0), rectangles[1].Position);
        Approximately.Equal(new Offset(0, 20), rectangles[2].Position);
        Approximately.Equal(new Offset(100, 20), rectangles[3].Position);
    }

    [Fact]
    public void SkipsSlotsClaimedByAColumnSpan()
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns =>
            {
                columns.Share();
                columns.Share();
                columns.Share();
            });

            Fill(descriptor.Cell().SpanColumns(2), 1, 20);
            Fill(descriptor.Cell(), 1, 20);
            Fill(descriptor.Cell(), 1, 20);
        });

        RecordedPage page = LayoutHarness.Draw(table, new Extent(300, 200));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        // The spanning cell occupies columns one and two, pushing the next cell to column three.
        Approximately.Equal(200f, rectangles[1].Position.X);

        // The following cell wraps to the start of the second row.
        Approximately.Equal(new Offset(0, 20), rectangles[2].Position);
    }

    [Fact]
    public void GivesASpanningCellTheCombinedColumnWidth()
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns =>
            {
                columns.Share();
                columns.Share();
            });

            descriptor.Cell().SpanColumns(2).Compose(inner => inner.Slot().Child = new ExpandBlock
            {
                ExtendHorizontal = true,
                Child = new FixedBlock(1, 10)
            });
        });

        // Measure before drawing: drawing consumes the table's rows and a later measurement would report Empty.
        Fit plan = LayoutHarness.Measure(table, new Extent(200, 200));
        RecordedPage page = LayoutHarness.Draw(table, new Extent(200, 200));

        Approximately.Equal(200f, plan.Size.Width);
        Assert.NotEmpty(page.Operations);
    }

    [Fact]
    public void RespectsExplicitCellPositions()
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns =>
            {
                columns.Share();
                columns.Share();
            });

            Fill(descriptor.Cell().AtRow(1).AtColumn(1), 1, 20);
            Fill(descriptor.Cell().AtRow(2).AtColumn(2), 1, 20);
        });

        RecordedPage page = LayoutHarness.Draw(table, new Extent(200, 200));
        RectangleOperation placed = page.Operations.OfType<RectangleOperation>().Last();

        Approximately.Equal(new Offset(100, 20), placed.Position);
    }

    [Fact]
    public void RowHeightFollowsItsTallestCell()
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns =>
            {
                columns.Share();
                columns.Share();
            });

            Fill(descriptor.Cell(), 1, 10);
            Fill(descriptor.Cell(), 1, 45);
        });

        Fit plan = LayoutHarness.Measure(table, new Extent(200, 200));

        Approximately.Equal(45f, plan.Size.Height);
    }

    [Fact]
    public void RepeatsHeaderCellsOnEveryPage()
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns => columns.Share());

            descriptor.HeaderRows(header => Fill(header.Cell(), 1, 20));

            for (int index = 0; index < 4; index++)
                Fill(descriptor.Cell(), 1, 30);
        });

        // 80pt leaves 60 for the body after the 20pt header, so two rows fit per page.
        Extent space = new Extent(200, 80);

        RecordedPage firstPage = LayoutHarness.Draw(table, space);
        RecordedPage secondPage = LayoutHarness.Draw(table, space);

        Assert.Equal(3, firstPage.Operations.OfType<RectangleOperation>().Count());
        Assert.Equal(3, secondPage.Operations.OfType<RectangleOperation>().Count());
    }

    [Fact]
    public void PositionsBodyRowsBelowTheHeader()
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns => columns.Share());

            descriptor.HeaderRows(header => Fill(header.Cell(), 1, 25));
            Fill(descriptor.Cell(), 1, 30);
        });

        RecordedPage page = LayoutHarness.Draw(table, new Extent(200, 200));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(0f, rectangles[0].Position.Y);
        Approximately.Equal(25f, rectangles[1].Position.Y);
    }

    [Fact]
    public void RepeatsHeaderTextOnEveryPage()
    {
        // Regression guard: header cells containing text, rather than a stateless fixture. Text remembers how
        // many of its lines it has drawn, so without a per-page reset the band renders once and then vanishes.
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns => columns.Share());

            descriptor.HeaderRows(header => header.Cell().Text("Code"));

            for (int index = 0; index < 4; index++)
                Fill(descriptor.Cell(), 1, 30);
        });

        // 12pt of header plus two 30pt rows fits in 72pt.
        Extent space = new Extent(200, 72);

        RecordedPage firstPage = LayoutHarness.Draw(table, space);
        RecordedPage secondPage = LayoutHarness.Draw(table, space);

        Assert.Equal("Code", firstPage.Content);
        Assert.Equal("Code", secondPage.Content);
    }

    [Fact]
    public void RepeatsFooterTextOnEveryPage()
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns => columns.Share());

            descriptor.FooterRows(footer => footer.Cell().Text("Total"));

            for (int index = 0; index < 4; index++)
                Fill(descriptor.Cell(), 1, 30);
        });

        Extent space = new Extent(200, 72);

        RecordedPage firstPage = LayoutHarness.Draw(table, space);
        RecordedPage secondPage = LayoutHarness.Draw(table, space);

        Assert.Equal("Total", firstPage.Content);
        Assert.Equal("Total", secondPage.Content);
    }

    [Fact]
    public void HeaderBandCanDifferPerPage()
    {
        // A "continued" marker suppressed on the opening page is the canonical reason a repeating band's height
        // varies. Caching band heights alongside the page-invariant body would freeze page one's answer and the
        // marker would never appear at all.
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns => columns.Share());

            descriptor.HeaderRows(header => header.Cell().SkipFirst().Text("continued"));

            for (int index = 0; index < 4; index++)
                Fill(descriptor.Cell(), 1, 30);
        });

        Extent space = new Extent(200, 72);

        RecordedPage firstPage = LayoutHarness.Draw(table, space);
        RecordedPage secondPage = LayoutHarness.Draw(table, space);

        Assert.Equal(string.Empty, firstPage.Content);
        Assert.Equal("continued", secondPage.Content);
    }

    [Fact]
    public void ReportsPartialRenderWhileRowsRemain()
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns => columns.Share());

            for (int index = 0; index < 4; index++)
                Fill(descriptor.Cell(), 1, 30);
        });

        Fit plan = LayoutHarness.Measure(table, new Extent(200, 60));

        Assert.True(plan.IsPartial);
        Approximately.Equal(60f, plan.Size.Height);
    }

    [Fact]
    public void ReportsEmptyOnceEveryRowIsDrawn()
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns => columns.Share());
            Fill(descriptor.Cell(), 1, 20);
        });

        Extent space = new Extent(200, 200);
        LayoutHarness.Draw(table, space);

        Assert.True(LayoutHarness.Measure(table, space).IsNothing);
    }

    [Fact]
    public void KeepsVerticallySpannedRowsTogether()
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns =>
            {
                columns.Share();
                columns.Share();
            });

            // A cell spanning both rows forbids a break between them.
            Fill(descriptor.Cell().AtRow(1).AtColumn(1).SpanRows(2), 1, 60);
            Fill(descriptor.Cell().AtRow(1).AtColumn(2), 1, 30);
            Fill(descriptor.Cell().AtRow(2).AtColumn(2), 1, 30);
        });

        // Only the first row would fit, but breaking inside the span is not allowed.
        Fit plan = LayoutHarness.Measure(table, new Extent(200, 40));

        Assert.True(plan.IsDeferred);
    }

    [Fact]
    public void WrapsWhenConstantColumnsExceedTheAvailableWidth()
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns => columns.Fixed(300));
            Fill(descriptor.Cell(), 1, 10);
        });

        Assert.True(LayoutHarness.Measure(table, new Extent(100, 200)).IsDeferred);
    }

    [Fact]
    public void WrapsWithoutAnyColumns()
    {
        TableBlock table = BuildTable(descriptor => Fill(descriptor.Cell(), 1, 10));

        Assert.True(LayoutHarness.Measure(table, new Extent(200, 200)).IsDeferred);
        Assert.Empty(LayoutHarness.Draw(table, new Extent(200, 200)).Operations);
    }

    [Fact]
    public void ARelativeColumnWithoutWeightGetsNoWidth()
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns =>
            {
                columns.Fixed(50);
                columns.Share(0);
            });

            Fill(descriptor.Cell(), 1, 10);
        });

        Approximately.Equal(50f, LayoutHarness.Measure(table, new Extent(200, 200)).Size.Width);
    }

    [Fact]
    public void WrapsWhenTheRepeatingBandsAloneExceedTheHeight()
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns => columns.Share());
            descriptor.HeaderRows(header => Fill(header.Cell(), 1, 50));
            descriptor.FooterRows(footer => Fill(footer.Cell(), 1, 50));
            Fill(descriptor.Cell(), 1, 10);
        });

        Fit plan = LayoutHarness.Measure(table, new Extent(200, 99));

        Assert.True(plan.IsDeferred);
        Assert.Contains("header and footer", plan.DeferReason);
    }

    [Fact]
    public void DrawsNothingWhileTheRepeatingBandsCannotFit()
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns => columns.Share());
            descriptor.HeaderRows(header => Fill(header.Cell(), 1, 50));
            descriptor.FooterRows(footer => Fill(footer.Cell(), 1, 50));
            Fill(descriptor.Cell(), 1, 10);
        });

        RecordedPage cramped = LayoutHarness.Draw(table, new Extent(200, 99));
        RecordedPage roomy = LayoutHarness.Draw(table, new Extent(200, 200));

        Assert.Empty(cramped.Operations);
        Assert.Equal(3, roomy.Operations.OfType<RectangleOperation>().Count());
    }

    [Fact]
    public void DrawsNothingOnceEveryRowIsDrawn()
    {
        // The header repeats alongside rows, never on its own.
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns => columns.Share());
            descriptor.HeaderRows(header => Fill(header.Cell(), 1, 10));
            Fill(descriptor.Cell(), 1, 20);
        });

        Extent space = new Extent(200, 200);
        LayoutHarness.Draw(table, space);

        Assert.Empty(LayoutHarness.Draw(table, space).Operations);
    }

    [Fact]
    public void LeavesARowTooTallForThePageForTheNextOne()
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns => columns.Share());
            descriptor.HeaderRows(header => Fill(header.Cell(), 1, 10, TestInks.Red));
            Fill(descriptor.Cell(), 1, 30);
            Fill(descriptor.Cell(), 1, 80, TestInks.Blue);
        });

        LayoutHarness.Draw(table, new Extent(200, 50));
        RecordedPage cramped = LayoutHarness.Draw(table, new Extent(200, 50));
        RecordedPage roomy = LayoutHarness.Draw(table, new Extent(200, 100));

        // No header on a page that takes no rows.
        Assert.Empty(cramped.Operations);

        Assert.Contains(roomy.Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
        Approximately.Equal(10f, roomy.Operations.OfType<RectangleOperation>().Single(r => r.Ink == TestInks.Blue).Position.Y);
    }

    [Fact]
    public void ASpannedCellTallerThanItsRowsGrowsOnlyTheLastOne()
    {
        // Charging the shortfall to the first row would push the second row down inside the span.
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns =>
            {
                columns.Share();
                columns.Share();
            });

            Fill(descriptor.Cell().AtRow(1).AtColumn(1).SpanRows(2), 1, 100);
            Fill(descriptor.Cell().AtRow(1).AtColumn(2), 1, 30);
            Fill(descriptor.Cell().AtRow(2).AtColumn(2), 1, 30, TestInks.Blue);
        });

        Extent space = new Extent(200, 200);
        Fit plan = LayoutHarness.Measure(table, space);
        RecordedPage page = LayoutHarness.Draw(table, space);

        Approximately.Equal(100f, plan.Size.Height);
        Approximately.Equal(30f, page.Operations.OfType<RectangleOperation>().Single(r => r.Ink == TestInks.Blue).Position.Y);
    }
}
