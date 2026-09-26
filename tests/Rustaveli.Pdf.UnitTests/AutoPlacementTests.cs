namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Where cells land once a table's composition is complete, observed as the (row, column) each cell resolved to.
/// </summary>
public class AutoPlacementTests
{
    private static TableElement Compose(int columns, Action<TableDescriptor> cells)
    {
        Block root = LayoutHarness.Build(container => container.Table(table =>
        {
            table.ColumnsDefinition(definition =>
            {
                for (int index = 0; index < columns; index++)
                    definition.RelativeColumn();
            });

            cells(table);
        }));

        return Assert.IsType<TableElement>(((Frame)root).Child);
    }

    private static IEnumerable<(int Row, int Column)> Slots(IEnumerable<TableCell> cells) =>
        cells.Select(cell => (cell.Row, cell.Column));

    private static IEnumerable<(int Row, int Column)> Place(int columns, Action<TableDescriptor> cells) =>
        Slots(Compose(columns, cells).Cells);

    [Fact]
    public void AutomaticCellsStepAroundACellPinnedOnBothAxes()
    {
        IEnumerable<(int Row, int Column)> slots = Place(2, table =>
        {
            table.Cell().Row(1).Column(1);
            table.Cell();
            table.Cell();
        });

        Assert.Equal(new[] { (1, 1), (1, 2), (2, 1) }, slots);
    }

    [Fact]
    public void AnAutomaticCellWrapsWhenTheRestOfItsRowIsClaimed()
    {
        IEnumerable<(int Row, int Column)> slots = Place(2, table =>
        {
            table.Cell().Row(1).Column(2);
            table.Cell();
            table.Cell();
        });

        Assert.Equal(new[] { (1, 2), (1, 1), (2, 1) }, slots);
    }

    [Fact]
    public void ASpanningAutomaticCellWrapsRatherThanOverhangingTheLastColumn()
    {
        IEnumerable<(int Row, int Column)> slots = Place(3, table =>
        {
            table.Cell();
            table.Cell();
            table.Cell().ColumnSpan(2);
        });

        // Column three is free, but a two-column span starting there would run past the grid.
        Assert.Equal(new[] { (1, 1), (1, 2), (2, 1) }, slots);
    }

    [Fact]
    public void AutomaticPlacementNeverBacktracksToASkippedSlot()
    {
        IEnumerable<(int Row, int Column)> slots = Place(4, table =>
        {
            table.Cell().Row(1).Column(2);
            table.Cell().ColumnSpan(2);
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
            table.Cell().RowSpan(2);
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
            table.Cell().Row(1).Column(1);
            table.Cell().Row(1);
        });

        Assert.Equal(new[] { (1, 1), (1, 2) }, slots);
    }

    [Fact]
    public void RowOnlyCellsDoNotStackOnOneAnother()
    {
        IEnumerable<(int Row, int Column)> slots = Place(2, table =>
        {
            table.Cell().Row(2);
            table.Cell().Row(2);
        });

        Assert.Equal(new[] { (2, 1), (2, 2) }, slots);
    }

    [Fact]
    public void ASpanningRowOnlyCellNeedsAGapAsWideAsItsSpan()
    {
        IEnumerable<(int Row, int Column)> slots = Place(4, table =>
        {
            table.Cell().Row(1).Column(2);
            table.Cell().Row(1).ColumnSpan(2);
        });

        // Column one is free but too narrow on its own, and column two is taken.
        Assert.Equal(new[] { (1, 2), (1, 3) }, slots);
    }

    [Fact]
    public void ARowOnlyCellKeepsItsRowSpanClear()
    {
        IEnumerable<(int Row, int Column)> slots = Place(2, table =>
        {
            table.Cell().Row(2).Column(1);
            table.Cell().Row(1).RowSpan(2);
        });

        Assert.Equal(new[] { (2, 1), (1, 2) }, slots);
    }

    [Fact]
    public void ARowOnlyCellInAFullRowIsKeptInsideTheGrid()
    {
        IEnumerable<(int Row, int Column)> slots = Place(3, table =>
        {
            table.Cell().Row(1).Column(1);
            table.Cell().Row(1).Column(2);
            table.Cell().Row(1).Column(3);
            table.Cell().Row(1).ColumnSpan(2);
        });

        // No slot is free, so the cell settles on the last column its span still fits from rather than being
        // pushed past the grid, where validation would reject it.
        Assert.Equal((1, 2), slots.Last());
    }

    [Fact]
    public void AColumnOnlyCellDropsToTheFirstFreeRow()
    {
        IEnumerable<(int Row, int Column)> slots = Place(2, table =>
        {
            table.Cell().Row(1).Column(2);
            table.Cell().Column(2);
        });

        Assert.Equal(new[] { (1, 2), (2, 2) }, slots);
    }

    [Fact]
    public void AColumnOnlyCellStartsAtTheTopWhenItsColumnIsFree()
    {
        IEnumerable<(int Row, int Column)> slots = Place(2, table =>
        {
            table.Cell().Column(2);
            table.Cell();
        });

        Assert.Equal(new[] { (1, 2), (1, 1) }, slots);
    }

    [Fact]
    public void AColumnOnlyCellAvoidsRowOnlyCells()
    {
        IEnumerable<(int Row, int Column)> slots = Place(2, table =>
        {
            table.Cell().Column(1);
            table.Cell().Row(1);
        });

        // Row-only cells are resolved first, so the column-only cell declared before it still gives way.
        Assert.Equal(new[] { (2, 1), (1, 1) }, slots);
    }

    [Fact]
    public void ASpanningColumnOnlyCellNeedsItsWholeSpanFree()
    {
        IEnumerable<(int Row, int Column)> slots = Place(2, table =>
        {
            table.Cell().Row(1).Column(2);
            table.Cell().Column(1).ColumnSpan(2);
        });

        Assert.Equal(new[] { (1, 2), (2, 1) }, slots);
    }

    [Fact]
    public void AColumnOnlyCellKeepsItsRowSpanClear()
    {
        IEnumerable<(int Row, int Column)> slots = Place(1, table =>
        {
            table.Cell().Row(2).Column(1);
            table.Cell().Column(1).RowSpan(2);
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
            table.Cell().Row(1);
            table.Cell().Column(1);
            table.Cell().Row(2).Column(2);
            table.Cell();
        });

        Assert.Equal(new[] { (1, 2), (1, 1), (2, 1), (2, 2), (3, 1) }, slots);
    }

    [Fact]
    public void EachBandIsPlacedOnItsOwnGrid()
    {
        TableElement table = Compose(2, descriptor =>
        {
            descriptor.Header(header =>
            {
                header.Cell();
                header.Cell();
            });

            descriptor.Cell();
            descriptor.Cell();
            descriptor.Cell();

            descriptor.Footer(footer =>
            {
                footer.Cell();
                footer.Cell();
            });
        });

        Assert.Equal(new[] { (1, 1), (1, 2) }, Slots(table.HeaderCells));
        Assert.Equal(new[] { (1, 1), (1, 2), (2, 1) }, Slots(table.Cells));
        Assert.Equal(new[] { (1, 1), (1, 2) }, Slots(table.FooterCells));
    }
}
