namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Grids of cells flowing into rows of equal columns, laid out 120 points wide.
/// </summary>
public class GridTests
{
    private static readonly Extent Space = new Extent(120, 400);

    private static List<RectangleOperation> Cells(Action<GridComposer> compose) =>
        LayoutHarness.Render(frame => frame.Grid(compose), Space).Operations.OfType<RectangleOperation>()
            .Where(operation => operation.Ink == TestInks.Red)
            .ToList();

    private static void Filled(IFrame cell) =>
        cell.Fill(TestInks.Red).Compose(inner => inner.Slot().Child = new FixedBlock(1, 10));

    [Fact]
    public void CellsFlowIntoRowsOfTheColumnsSet()
    {
        List<RectangleOperation> cells = Cells(grid =>
        {
            grid.Columns(3);

            for (int index = 0; index < 5; index++)
                Filled(grid.Cell());
        });

        Assert.Equal(5, cells.Count);
        Assert.Equal(new Bounds(0, 0, 40, 10), cells[0].Bounds);
        Assert.Equal(new Bounds(40, 0, 80, 10), cells[1].Bounds);
        Assert.Equal(new Bounds(80, 0, 120, 10), cells[2].Bounds);
        Assert.Equal(new Bounds(0, 10, 40, 20), cells[3].Bounds);
        Assert.Equal(new Bounds(40, 10, 80, 20), cells[4].Bounds);
    }

    [Fact]
    public void ACellSpansTheColumnsItAsksFor()
    {
        List<RectangleOperation> cells = Cells(grid =>
        {
            grid.Columns(4);
            Filled(grid.Cell(3));
            Filled(grid.Cell(2));
        });

        // Three of four columns, then a row of its own for the cell that would not fit beside it.
        Assert.Equal(90f, cells[0].Size.Width, 3);
        Assert.Equal(60f, cells[1].Size.Width, 3);
        Assert.True(cells[1].Position.Y > cells[0].Position.Y);
    }

    [Fact]
    public void TwelveColumnsUnlessSet()
    {
        List<RectangleOperation> cells = Cells(grid =>
        {
            Filled(grid.Cell(6));
            Filled(grid.Cell(6));
        });

        Assert.Equal(cells[0].Position.Y, cells[1].Position.Y);
        Assert.Equal(60f, cells[0].Size.Width, 3);
    }

    [Fact]
    public void GuttersAndRowSpacingSeparateTheCells()
    {
        List<RectangleOperation> cells = Cells(grid =>
        {
            grid.Columns(2);
            grid.Gutter(20);
            grid.SpaceBetweenRows(5);

            for (int index = 0; index < 3; index++)
                Filled(grid.Cell());
        });

        Assert.Equal(new Bounds(0, 0, 50, 10), cells[0].Bounds);
        Assert.Equal(new Bounds(70, 0, 120, 10), cells[1].Bounds);
        Assert.Equal(new Bounds(0, 15, 50, 25), cells[2].Bounds);
    }

    [Fact]
    public void ColumnsLineUpFromRowToRow()
    {
        List<RectangleOperation> cells = Cells(grid =>
        {
            grid.Columns(4);
            grid.Gutter(8);
            Filled(grid.Cell());
            Filled(grid.Cell());
            Filled(grid.Cell(2));

            for (int index = 0; index < 4; index++)
                Filled(grid.Cell());
        });

        // Columns of 24 with gutters of 8: the wide cell covers the last two columns and the gutter between them.
        Assert.Equal(new Bounds(0, 0, 24, 10), cells[0].Bounds);
        Assert.Equal(new Bounds(64, 0, 120, 10), cells[2].Bounds);
        Assert.Equal(new Bounds(0, 10, 24, 20), cells[3].Bounds);
        Assert.Equal(new Bounds(64, 10, 88, 20), cells[5].Bounds);
        Assert.Equal(new Bounds(96, 10, 120, 20), cells[6].Bounds);
    }

    [Fact]
    public void ACentredRowStaysOnTheColumns()
    {
        List<RectangleOperation> cells = Cells(grid =>
        {
            grid.Columns(4);
            grid.Gutter(8);
            grid.Centered();
            Filled(grid.Cell(2));
        });

        Assert.Equal(new Bounds(32, 0, 88, 10), Assert.Single(cells).Bounds);
    }

    [Fact]
    public void EveryCellInARowIsAsTallAsTheTallest()
    {
        List<RectangleOperation> fills = LayoutHarness.Render(frame => frame.Grid(grid =>
        {
            grid.Columns(2);
            Filled(grid.Cell());
            grid.Cell().Fill(TestInks.Red).Compose(inner => inner.Slot().Child = new FixedBlock(1, 30));
        }), Space).Operations.OfType<RectangleOperation>().Where(operation => operation.Ink == TestInks.Red).ToList();

        Assert.All(fills, fill => Assert.Equal(30f, fill.Size.Height, 3));
    }

    [Theory]
    [InlineData("left", 0f)]
    [InlineData("centre", 40f)]
    [InlineData("right", 80f)]
    public void ARowTheCellsDoNotFillIsPlacedAsTheGridSays(string placement, float left)
    {
        List<RectangleOperation> cells = Cells(grid =>
        {
            grid.Columns(3);

            if (placement == "centre")
                grid.Centered();
            else if (placement == "right")
                grid.FlushRight();
            else
                grid.FlushLeft();

            Filled(grid.Cell());
        });

        Assert.Equal(left, Assert.Single(cells).Position.X, 3);
    }

    [Fact]
    public void AGridWithNoCellsIsEmpty() =>
        Assert.Empty(LayoutHarness.Render(frame => frame.Grid(grid => grid.Columns(3)), Space).Operations);

    [Fact]
    public void ACellSpansAtLeastOneColumn()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LayoutHarness.Build(frame => frame.Grid(grid => grid.Cell(0))));
        Assert.Throws<ArgumentOutOfRangeException>(() => LayoutHarness.Build(frame => frame.Grid(grid => grid.Columns(0))));
        Assert.Throws<ArgumentNullException>(() => LayoutHarness.Build(frame => frame.Grid(null!)));
    }

    [Fact]
    public void ACellCannotSpanMoreColumnsThanTheGridHas()
    {
        CompositionException exception = Assert.Throws<CompositionException>(() => LayoutHarness.Build(frame => frame.Grid(grid =>
        {
            grid.Columns(3);
            grid.Cell(4);
        })));

        Assert.Equal("A cell spans 4 columns of a grid of 3.", exception.Message);
    }
}
