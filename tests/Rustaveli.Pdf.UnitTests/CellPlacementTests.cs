namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Where cells land once a table's composition is complete, observed as the (row, column) each cell resolved to.
/// </summary>
public class CellPlacementTests
{
    private static TableBlock Compose(int columns, Action<TableComposer> cells)
    {
        Block root = LayoutHarness.Build(frame => frame.Table(table =>
        {
            table.Columns(definition =>
            {
                for (int index = 0; index < columns; index++)
                    definition.Share();
            });

            cells(table);
        }));

        return Assert.IsType<TableBlock>(((Frame)root).Child);
    }

    private static IEnumerable<(int Row, int Column)> Slots(IEnumerable<CellBlock> cells) =>
        cells.Select(cell => (cell.Row, cell.Column));

    private static IEnumerable<(int Row, int Column)> Place(int columns, Action<TableComposer> cells) =>
        Slots(Compose(columns, cells).Cells);

    [Fact]
    public void AutomaticCellsStepAroundACellPinnedOnBothAxes()
    {
        IEnumerable<(int Row, int Column)> slots = Place(2, table =>
        {
            table.Cell().AtRow(1).AtColumn(1);
            table.Cell();
            table.Cell();
        });

        Assert.Equal(new[] { (1, 1), (1, 2), (2, 1) }, slots);
    }

    [Fact]
    public void AnAutomaticCellDefersWhenTheRestOfItsRowIsClaimed()
    {
        IEnumerable<(int Row, int Column)> slots = Place(2, table =>
        {
            table.Cell().AtRow(1).AtColumn(2);
            table.Cell();
            table.Cell();
        });

        Assert.Equal(new[] { (1, 2), (1, 1), (2, 1) }, slots);
    }

    [Fact]
    public void ASpanningAutomaticCellDefersRatherThanOverhangingTheLastColumn()
    {
        IEnumerable<(int Row, int Column)> slots = Place(3, table =>
        {
            table.Cell();
            table.Cell();
            table.Cell().SpanColumns(2);
        });

        // Column three is free, but a two-column span starting there would run past the grid.
        Assert.Equal(new[] { (1, 1), (1, 2), (2, 1) }, slots);
    }

    [Fact]
    public void AutomaticPlacementNeverBacktracksToASkippedSlot()
    {
        IEnumerable<(int Row, int Column)> slots = Place(4, table =>
        {
            table.Cell().AtRow(1).AtColumn(2);
            table.Cell().SpanColumns(2);
            table.Cell();
        });

        // The spanning cell could not start in column one, so it passed it by. Placement moves forward only, so
        // the next cell starts the following row rather than filling that gap out of order.
        Assert.Equal(new[] { (1, 2), (1, 3), (2, 1) }, slots);
    }

    [Fact]
    public void AnAutomaticCellsRowSpanClaimsTheSlotBelowIt()
    {
        IEnumerable<(int Row, int Column)> slots = Place(2, table =>
        {
            table.Cell().SpanRows(2);
            table.Cell();
            table.Cell();
        });

        Assert.Equal(new[] { (1, 1), (1, 2), (2, 2) }, slots);
    }

    [Fact]
    public void ARowOnlyCellTakesTheFirstFreeColumnOfItsRow()
    {
        IEnumerable<(int Row, int Column)> slots = Place(3, table =>
        {
            table.Cell().AtRow(1).AtColumn(1);
            table.Cell().AtRow(1);
        });

        Assert.Equal(new[] { (1, 1), (1, 2) }, slots);
    }

    [Fact]
    public void RowOnlyCellsDoNotStackOnOneAnother()
    {
        IEnumerable<(int Row, int Column)> slots = Place(2, table =>
        {
            table.Cell().AtRow(2);
            table.Cell().AtRow(2);
        });

        Assert.Equal(new[] { (2, 1), (2, 2) }, slots);
    }

    [Fact]
    public void ASpanningRowOnlyCellNeedsAGapAsWideAsItsSpan()
    {
        IEnumerable<(int Row, int Column)> slots = Place(4, table =>
        {
            table.Cell().AtRow(1).AtColumn(2);
            table.Cell().AtRow(1).SpanColumns(2);
        });

        // Column one is free but too narrow on its own, and column two is taken.
        Assert.Equal(new[] { (1, 2), (1, 3) }, slots);
    }

    [Fact]
    public void ARowOnlyCellKeepsItsRowSpanClear()
    {
        IEnumerable<(int Row, int Column)> slots = Place(2, table =>
        {
            table.Cell().AtRow(2).AtColumn(1);
            table.Cell().AtRow(1).SpanRows(2);
        });

        Assert.Equal(new[] { (2, 1), (1, 2) }, slots);
    }

    [Fact]
    public void ARowWithNoRoomLeftRefusesACellPinnedToIt()
    {
        // Moving the cell to another row would not put it where it was asked, and keeping it would cover a cell.
        CompositionException full = Assert.Throws<CompositionException>(() => Place(3, table =>
        {
            table.Cell().AtRow(1).AtColumn(1);
            table.Cell().AtRow(1).AtColumn(2);
            table.Cell().AtRow(1).AtColumn(3);
            table.Cell().AtRow(1);
        }));

        Assert.Contains("body cell is placed in row 1", full.Message, StringComparison.Ordinal);
        Assert.Contains("1-column span", full.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARowWithGapsTooNarrowRefusesASpanningCellPinnedToIt()
    {
        CompositionException narrow = Assert.Throws<CompositionException>(() => Compose(3, table => table.HeaderRows(header =>
        {
            header.Cell().AtRow(1).AtColumn(2);
            header.Cell().AtRow(1).SpanColumns(2);
        })));

        Assert.Contains("header cell is placed in row 1", narrow.Message, StringComparison.Ordinal);
        Assert.Contains("2-column span", narrow.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AColumnOnlyCellDropsToTheFirstFreeRow()
    {
        IEnumerable<(int Row, int Column)> slots = Place(2, table =>
        {
            table.Cell().AtRow(1).AtColumn(2);
            table.Cell().AtColumn(2);
        });

        Assert.Equal(new[] { (1, 2), (2, 2) }, slots);
    }

    [Fact]
    public void AColumnOnlyCellStartsAtTheTopWhenItsColumnIsFree()
    {
        IEnumerable<(int Row, int Column)> slots = Place(2, table =>
        {
            table.Cell().AtColumn(2);
            table.Cell();
        });

        Assert.Equal(new[] { (1, 2), (1, 1) }, slots);
    }

    [Fact]
    public void AColumnOnlyCellAvoidsRowOnlyCells()
    {
        IEnumerable<(int Row, int Column)> slots = Place(2, table =>
        {
            table.Cell().AtColumn(1);
            table.Cell().AtRow(1);
        });

        // Row-only cells are resolved first, so the column-only cell declared before it still gives way.
        Assert.Equal(new[] { (2, 1), (1, 1) }, slots);
    }

    [Fact]
    public void ASpanningColumnOnlyCellNeedsItsWholeSpanFree()
    {
        IEnumerable<(int Row, int Column)> slots = Place(2, table =>
        {
            table.Cell().AtRow(1).AtColumn(2);
            table.Cell().AtColumn(1).SpanColumns(2);
        });

        Assert.Equal(new[] { (1, 2), (2, 1) }, slots);
    }

    [Fact]
    public void AColumnOnlyCellKeepsItsRowSpanClear()
    {
        IEnumerable<(int Row, int Column)> slots = Place(1, table =>
        {
            table.Cell().AtRow(2).AtColumn(1);
            table.Cell().AtColumn(1).SpanRows(2);
        });

        // Rows one and three are free, but a two-row span starting at either would cross row two.
        Assert.Equal(new[] { (2, 1), (3, 1) }, slots);
    }

    [Fact]
    public void AutomaticCellsFillAroundEveryKindOfExplicitPlacement()
    {
        IEnumerable<(int Row, int Column)> slots = Place(2, table =>
        {
            table.Cell();
            table.Cell().AtRow(1);
            table.Cell().AtColumn(1);
            table.Cell().AtRow(2).AtColumn(2);
            table.Cell();
        });

        Assert.Equal(new[] { (1, 2), (1, 1), (2, 1), (2, 2), (3, 1) }, slots);
    }

    [Fact]
    public void EachBandIsPlacedOnItsOwnGrid()
    {
        TableBlock table = Compose(2, composer =>
        {
            composer.HeaderRows(header =>
            {
                header.Cell();
                header.Cell();
            });

            composer.Cell();
            composer.Cell();
            composer.Cell();

            composer.FooterRows(footer =>
            {
                footer.Cell();
                footer.Cell();
            });
        });

        Assert.Equal(new[] { (1, 1), (1, 2) }, Slots(table.HeaderCells));
        Assert.Equal(new[] { (1, 1), (1, 2), (2, 1) }, Slots(table.Cells));
        Assert.Equal(new[] { (1, 1), (1, 2) }, Slots(table.FooterCells));
    }

#if NET
    [Fact]
    public void PlacingCellsThatEachTakeOneRowKeepsNoRecordOfWhereTheyWent()
    {
        // Allocation budget: automatic cells are placed in order and the cursor never returns to a slot it has
        // passed, so one that takes a single row needs no record. A table of 10,000 rows recorded all 30,000 of its
        // slots, the record's arrays on the large object heap.
        const long Budget = 1_024;
        List<CellBlock> cells = [.. Enumerable.Range(0, 3_000).Select(_ => new CellBlock())];
        CellPlacement.Apply(cells, columnCount: 3, band: "body");

        long before = GC.GetAllocatedBytesForCurrentThread();
        CellPlacement.Apply(cells, columnCount: 3, band: "body");
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal((1_000, 3), (cells[^1].Row, cells[^1].Column));
        Assert.True(allocated <= Budget, $"Placing 3,000 cells allocated {allocated:N0} bytes; its budget is {Budget:N0}.");
    }
#endif
}
