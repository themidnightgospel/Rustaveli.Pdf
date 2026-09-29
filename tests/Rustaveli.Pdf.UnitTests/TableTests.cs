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
    public void ARightToLeftTableMirrorsEveryColumnNotOnlyTheFirst()
    {
        RecordedPage page = LayoutHarness.Draw(container => container.RightToLeft().Table(table =>
        {
            table.Columns(columns =>
            {
                columns.Fixed(30);
                columns.Fixed(50);
                columns.Share();
            });

            Fill(table.Cell(), 1, 10);
            Fill(table.Cell(), 1, 10);
            Fill(table.Cell(), 1, 10);
        }), new Extent(200, 200));

        // Column one against the right edge, each next one to its left, each at its own width.
        Assert.Equal([170f, 120f, 0f], page.Operations.OfType<RectangleOperation>().Select(rectangle => rectangle.Position.X));
    }

    [Theory]
    [InlineData(300f, 0f, 0f, 10f, "The table columns do not fit within the available width.")]
    [InlineData(10f, 60f, 60f, 10f, "The header and footer rows alone exceed the available height.")]
    [InlineData(10f, 0f, 0f, 150f, "The next table row is taller than the available height.")]
    public void ATableThatCannotStartSaysWhy(float columnWidth, float headerHeight, float footerHeight, float rowHeight, string reason)
    {
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns => columns.Fixed(columnWidth));

            if (headerHeight > 0)
                descriptor.HeaderRows(header => Fill(header.Cell(), 1, headerHeight));

            if (footerHeight > 0)
                descriptor.FooterRows(footer => Fill(footer.Cell(), 1, footerHeight));

            Fill(descriptor.Cell(), 1, rowHeight);
        });

        Fit plan = LayoutHarness.Measure(table, new Extent(200, 100));

        Assert.True(plan.IsDeferred);
        Assert.Equal(reason, plan.DeferReason);
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
                Horizontally = true,
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
    public void ReportsPartialWhileRowsRemain()
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
        // Composing refuses cells without columns; a block holding them anyway still wraps rather than failing.
        TableBlock table = new TableBlock();
        Fill(new TableComposer(table).Cell(), 1, 10);

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

    [Theory]
    [InlineData("body", "row 2, column 2 of the body")]
    [InlineData("header", "row 1, column 2 of the header")]
    [InlineData("span", "row 3, column 2 of the body")]
    public void ACellThatCannotBeSetInItsColumnIsReportedRatherThanDropped(string where, string named)
    {
        // Content wider than its column cannot be drawn in it at any height. Left out of the row's height, it was
        // drawn in a row too short to hold anything, and lost without a word.
        Document document = Document.Compose(container => container.Section(page =>
        {
            page.Trim = new Extent(200, 100);
            page.Body().Table(table =>
            {
                table.Columns(columns =>
                {
                    columns.Fixed(40);
                    columns.Fixed(80);
                });

                if (where == "header")
                {
                    table.HeaderRows(rows =>
                    {
                        Fill(rows.Cell(), 10, 10);
                        Fill(rows.Cell(), 120, 10);
                    });
                }

                Fill(table.Cell(), 10, 10);
                Fill(table.Cell(), 10, 10);
                Fill(table.Cell(), 10, 10);
                Fill(table.Cell(), where == "body" ? 120 : 10, 10);

                if (where == "span")
                {
                    Fill(table.Cell().AtRow(3).AtColumn(1).SpanRows(2), 10, 10);
                    Fill(table.Cell().AtRow(3).AtColumn(2).SpanRows(2), 120, 10);
                }
            });
        }));

        OversetException exception = Assert.Throws<OversetException>(() => LayoutHarness.Render(document));

        Assert.Contains(named, exception.Message);
    }

    [Fact]
    public void ATableOfFixedColumnsIsLaidOutOnceWhateverWidthItIsOffered()
    {
        // Centred, or on a page sized to it, the table is measured in the whole width and drawn in its own, narrower
        // one. The columns come out the same, so the cells need not be measured again.
        CountingBlock cell = new CountingBlock();
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns =>
            {
                columns.Fixed(40);
                columns.Fixed(60);
            });

            descriptor.Cell().Compose(inner => inner.Slot().Child = cell);
        });

        LayoutHarness.Measure(table, new Extent(200, 100));
        int measured = cell.Plans;

        LayoutHarness.Measure(table, new Extent(100, 100));
        LayoutHarness.Measure(table, new Extent(200, 100));

        Assert.Equal(measured, cell.Plans);
    }

    [Fact]
    public void ATableIsLaidOutAgainWhenItsColumnsChange()
    {
        CountingBlock cell = new CountingBlock();
        TableBlock table = BuildTable(descriptor =>
        {
            descriptor.Columns(columns => columns.Share());
            descriptor.Cell().Compose(inner => inner.Slot().Child = cell);
        });

        LayoutHarness.Measure(table, new Extent(200, 100));
        int measured = cell.Plans;

        LayoutHarness.Measure(table, new Extent(100, 100));
        int narrower = cell.Plans;

        PlanContext rightToLeft = LayoutHarness.Context();
        rightToLeft.ReadingDirection = ReadingDirection.RightToLeft;
        LayoutHarness.Measure(table, new Extent(100, 100), rightToLeft);

        Assert.True(narrower > measured);
        Assert.True(cell.Plans > narrower);
    }

    [Fact]
    public void ACellSizedByTheHeightOfItsRowIsDrawnInIt()
    {
        // Content that takes its width from the height it is given cannot be measured in unlimited height, but it
        // fits the row the other cells make, and is drawn there.
        RecordedPage page = LayoutHarness.Draw(container => container.Table(table =>
        {
            table.Columns(columns =>
            {
                columns.Fixed(40);
                columns.Fixed(80);
            });

            Fill(table.Cell(), 10, 30);
            table.Cell().Proportion(2, ProportionFit.Height).Compose(inner => inner.Slot().Child = new FixedBlock(10, 10, TestInks.Red));
        }), new Extent(200, 100));

        Assert.Contains(page.Operations.OfType<RectangleOperation>(), r => r.Ink == TestInks.Red);
    }

    /// <summary>Content of a fixed size that counts how often it is measured.</summary>
    private sealed class CountingBlock : Block
    {
        public int Plans { get; private set; }

        protected override Fit PlanCore(Extent availableSpace, PlanContext context)
        {
            Plans++;
            return Fit.Complete(10, 10);
        }

        protected override void RenderCore(Extent availableSpace, RenderContext context)
        {
        }
    }
}
