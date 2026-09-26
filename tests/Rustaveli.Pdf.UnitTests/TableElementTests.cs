namespace Rustaveli.Pdf.UnitTests;

public class TableElementTests
{
    private static TableElement BuildTable(Action<TableComposer> compose)
    {
        TableElement element = new TableElement();
        TableComposer descriptor = new TableComposer(element);
        compose(descriptor);
        descriptor.PlaceAutomaticCells();
        return element;
    }

    private static void Fill(IFrame container, float width, float height) =>
        container.Element(inner => inner.Child = new FixedElement(width, height));

    private static void Fill(IFrame container, float width, float height, Ink color) =>
        container.Element(inner => inner.Child = new FixedElement(width, height, color));

    [Fact]
    public void SplitsWidthEvenlyBetweenEqualRelativeColumns()
    {
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
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
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1);
                columns.RelativeColumn(4);
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
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(30);
                columns.RelativeColumn();
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
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
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
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            Fill(descriptor.Cell().ColumnSpan(2), 1, 20);
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
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            descriptor.Cell().ColumnSpan(2).Element(inner => inner.Child = new ExpandBlock
            {
                ExtendHorizontal = true,
                Child = new FixedElement(1, 10)
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
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            Fill(descriptor.Cell().Row(1).Column(1), 1, 20);
            Fill(descriptor.Cell().Row(2).Column(2), 1, 20);
        });

        RecordedPage page = LayoutHarness.Draw(table, new Extent(200, 200));
        RectangleOperation placed = page.Operations.OfType<RectangleOperation>().Last();

        Approximately.Equal(new Offset(100, 20), placed.Position);
    }

    [Fact]
    public void RowHeightFollowsItsTallestCell()
    {
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
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
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns => columns.RelativeColumn());

            descriptor.Header(header => Fill(header.Cell(), 1, 20));

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
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns => columns.RelativeColumn());

            descriptor.Header(header => Fill(header.Cell(), 1, 25));
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
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns => columns.RelativeColumn());

            descriptor.Header(header => header.Cell().Text("Code"));

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
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns => columns.RelativeColumn());

            descriptor.Footer(footer => footer.Cell().Text("Total"));

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
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns => columns.RelativeColumn());

            descriptor.Header(header => header.Cell().SkipFirst().Text("continued"));

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
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns => columns.RelativeColumn());

            for (int index = 0; index < 4; index++)
                Fill(descriptor.Cell(), 1, 30);
        });

        Fit plan = LayoutHarness.Measure(table, new Extent(200, 60));

        Assert.True(plan.IsPartialRender);
        Approximately.Equal(60f, plan.Size.Height);
    }

    [Fact]
    public void ReportsEmptyOnceEveryRowIsDrawn()
    {
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns => columns.RelativeColumn());
            Fill(descriptor.Cell(), 1, 20);
        });

        Extent space = new Extent(200, 200);
        LayoutHarness.Draw(table, space);

        Assert.True(LayoutHarness.Measure(table, space).IsEmpty);
    }

    [Fact]
    public void KeepsVerticallySpannedRowsTogether()
    {
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            // A cell spanning both rows forbids a break between them.
            Fill(descriptor.Cell().Row(1).Column(1).RowSpan(2), 1, 60);
            Fill(descriptor.Cell().Row(1).Column(2), 1, 30);
            Fill(descriptor.Cell().Row(2).Column(2), 1, 30);
        });

        // Only the first row would fit, but breaking inside the span is not allowed.
        Fit plan = LayoutHarness.Measure(table, new Extent(200, 40));

        Assert.True(plan.IsWrap);
    }

    [Fact]
    public void WrapsWhenConstantColumnsExceedTheAvailableWidth()
    {
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns => columns.ConstantColumn(300));
            Fill(descriptor.Cell(), 1, 10);
        });

        Assert.True(LayoutHarness.Measure(table, new Extent(100, 200)).IsWrap);
    }

    [Fact]
    public void WrapsWithoutAnyColumns()
    {
        TableElement table = BuildTable(descriptor => Fill(descriptor.Cell(), 1, 10));

        Assert.True(LayoutHarness.Measure(table, new Extent(200, 200)).IsWrap);
        Assert.Empty(LayoutHarness.Draw(table, new Extent(200, 200)).Operations);
    }

    [Fact]
    public void ARelativeColumnWithoutWeightGetsNoWidth()
    {
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(50);
                columns.RelativeColumn(0);
            });

            Fill(descriptor.Cell(), 1, 10);
        });

        Approximately.Equal(50f, LayoutHarness.Measure(table, new Extent(200, 200)).Size.Width);
    }

    [Fact]
    public void WrapsWhenTheRepeatingBandsAloneExceedTheHeight()
    {
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns => columns.RelativeColumn());
            descriptor.Header(header => Fill(header.Cell(), 1, 50));
            descriptor.Footer(footer => Fill(footer.Cell(), 1, 50));
            Fill(descriptor.Cell(), 1, 10);
        });

        Fit plan = LayoutHarness.Measure(table, new Extent(200, 99));

        Assert.True(plan.IsWrap);
        Assert.Contains("header and footer", plan.WrapReason);
    }

    [Fact]
    public void DrawsNothingWhileTheRepeatingBandsCannotFit()
    {
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns => columns.RelativeColumn());
            descriptor.Header(header => Fill(header.Cell(), 1, 50));
            descriptor.Footer(footer => Fill(footer.Cell(), 1, 50));
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
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns => columns.RelativeColumn());
            descriptor.Header(header => Fill(header.Cell(), 1, 10));
            Fill(descriptor.Cell(), 1, 20);
        });

        Extent space = new Extent(200, 200);
        LayoutHarness.Draw(table, space);

        Assert.Empty(LayoutHarness.Draw(table, space).Operations);
    }

    [Fact]
    public void LeavesARowTooTallForThePageForTheNextOne()
    {
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns => columns.RelativeColumn());
            descriptor.Header(header => Fill(header.Cell(), 1, 10, TestInks.Red));
            Fill(descriptor.Cell(), 1, 30);
            Fill(descriptor.Cell(), 1, 80, TestInks.Blue);
        });

        LayoutHarness.Draw(table, new Extent(200, 50));
        RecordedPage cramped = LayoutHarness.Draw(table, new Extent(200, 50));
        RecordedPage roomy = LayoutHarness.Draw(table, new Extent(200, 100));

        // No header on a page that takes no rows.
        Assert.Empty(cramped.Operations);

        Assert.Contains(roomy.Operations.OfType<RectangleOperation>(), r => r.Color == TestInks.Red);
        Approximately.Equal(10f, roomy.Operations.OfType<RectangleOperation>().Single(r => r.Color == TestInks.Blue).Position.Y);
    }

    [Fact]
    public void ASpannedCellTallerThanItsRowsGrowsOnlyTheLastOne()
    {
        // Charging the shortfall to the first row would push the second row down inside the span.
        TableElement table = BuildTable(descriptor =>
        {
            descriptor.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            Fill(descriptor.Cell().Row(1).Column(1).RowSpan(2), 1, 100);
            Fill(descriptor.Cell().Row(1).Column(2), 1, 30);
            Fill(descriptor.Cell().Row(2).Column(2), 1, 30, TestInks.Blue);
        });

        Extent space = new Extent(200, 200);
        Fit plan = LayoutHarness.Measure(table, space);
        RecordedPage page = LayoutHarness.Draw(table, space);

        Approximately.Equal(100f, plan.Size.Height);
        Approximately.Equal(30f, page.Operations.OfType<RectangleOperation>().Single(r => r.Color == TestInks.Blue).Position.Y);
    }
}
