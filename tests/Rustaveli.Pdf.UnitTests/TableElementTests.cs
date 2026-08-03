namespace Rustaveli.Pdf.UnitTests;

public class TableElementTests
{
    private static TableElement BuildTable(Action<TableDescriptor> compose)
    {
        TableElement element = new TableElement();
        TableDescriptor descriptor = new TableDescriptor(element);
        compose(descriptor);
        descriptor.PlaceAutomaticCells();
        return element;
    }

    private static void Fill(IContainer container, float width, float height) =>
        container.Element(inner => inner.Child = new FixedElement(width, height));

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

        RecordedPage page = LayoutHarness.Draw(table, new Size(200, 200));
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

        RecordedPage page = LayoutHarness.Draw(table, new Size(200, 200));
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

        RecordedPage page = LayoutHarness.Draw(table, new Size(200, 200));
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

        RecordedPage page = LayoutHarness.Draw(table, new Size(200, 200));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        Approximately.Equal(new Position(0, 0), rectangles[0].Position);
        Approximately.Equal(new Position(100, 0), rectangles[1].Position);
        Approximately.Equal(new Position(0, 20), rectangles[2].Position);
        Approximately.Equal(new Position(100, 20), rectangles[3].Position);
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

        RecordedPage page = LayoutHarness.Draw(table, new Size(300, 200));
        List<RectangleOperation> rectangles = page.Operations.OfType<RectangleOperation>().ToList();

        // The spanning cell occupies columns one and two, pushing the next cell to column three.
        Approximately.Equal(200f, rectangles[1].Position.X);

        // The following cell wraps to the start of the second row.
        Approximately.Equal(new Position(0, 20), rectangles[2].Position);
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

            descriptor.Cell().ColumnSpan(2).Element(inner => inner.Child = new ExtendElement
            {
                ExtendHorizontal = true,
                Child = new FixedElement(1, 10)
            });
        });

        // Measure before drawing: drawing consumes the table's rows and a later measurement would report Empty.
        SpacePlan plan = LayoutHarness.Measure(table, new Size(200, 200));
        RecordedPage page = LayoutHarness.Draw(table, new Size(200, 200));

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

        RecordedPage page = LayoutHarness.Draw(table, new Size(200, 200));
        RectangleOperation placed = page.Operations.OfType<RectangleOperation>().Last();

        Approximately.Equal(new Position(100, 20), placed.Position);
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

        SpacePlan plan = LayoutHarness.Measure(table, new Size(200, 200));

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
        Size space = new Size(200, 80);

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

        RecordedPage page = LayoutHarness.Draw(table, new Size(200, 200));
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
        Size space = new Size(200, 72);

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

        Size space = new Size(200, 72);

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

            descriptor.Header(header => header.Cell().SkipOnce().Text("continued"));

            for (int index = 0; index < 4; index++)
                Fill(descriptor.Cell(), 1, 30);
        });

        Size space = new Size(200, 72);

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

        SpacePlan plan = LayoutHarness.Measure(table, new Size(200, 60));

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

        Size space = new Size(200, 200);
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
        SpacePlan plan = LayoutHarness.Measure(table, new Size(200, 40));

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

        Assert.True(LayoutHarness.Measure(table, new Size(100, 200)).IsWrap);
    }
}
