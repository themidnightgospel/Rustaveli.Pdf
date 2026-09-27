namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// The last cell of each column stretched to the bottom of the table, in a table whose first column ends a row
/// before its second.
/// </summary>
public class TableLastCellsTests
{
    private static List<RectangleOperation> Fills(bool extend, Extent? space = null, int rows = 3)
    {
        Block root = LayoutHarness.Build(frame => frame.Table(table =>
        {
            table.Columns(columns =>
            {
                columns.Share();
                columns.Share();
            });

            if (extend)
                table.ExtendLastCellsToBottom();

            for (int row = 1; row <= rows; row++)
            {
                if (row < rows)
                    table.Cell().AtRow(row).AtColumn(1).Fill(TestInks.Red).Height(10).Blank();

                table.Cell().AtRow(row).AtColumn(2).Fill(TestInks.White).Height(10).Blank();
            }
        }));

        return LayoutHarness.Draw(root, space ?? new Extent(100, 100)).Operations.OfType<RectangleOperation>()
            .Where(operation => operation.Ink == TestInks.Red)
            .ToList();
    }

    [Fact]
    public void WithoutStretchingAColumnEndsWhereItsCellsDo()
    {
        List<RectangleOperation> fills = Fills(extend: false);

        Assert.All(fills, fill => Assert.Equal(10f, fill.Size.Height));
    }

    [Fact]
    public void TheLastCellOfAColumnReachesTheBottomOfTheTable()
    {
        List<RectangleOperation> fills = Fills(extend: true);

        Assert.Equal(10f, fills[0].Size.Height);
        Assert.Equal(20f, fills[1].Size.Height);
    }

    [Fact]
    public void OnAPageTheLastCellReachesTheLastRowDrawnThere()
    {
        // Three rows of the first column's two, and six of the second; the first page holds four rows.
        List<RectangleOperation> fills = Fills(extend: true, new Extent(100, 40), rows: 6);

        Assert.Equal(4, fills.Count);
        Assert.Equal(10f, fills[3].Size.Height);
    }

    [Fact]
    public void ACellWithAnotherBelowItInAnyColumnItSpansIsNotLast()
    {
        Block root = LayoutHarness.Build(frame => frame.Table(table =>
        {
            table.Columns(columns =>
            {
                columns.Share();
                columns.Share();
            });
            table.ExtendLastCellsToBottom();
            table.Cell().AtRow(1).AtColumn(1).SpanColumns(2).Fill(TestInks.Red).Height(10).Blank();
            table.Cell().AtRow(2).AtColumn(2).Height(10).Blank();
            table.Cell().AtRow(3).AtColumn(2).Height(10).Blank();
        }));

        RectangleOperation spanning = LayoutHarness.Draw(root, new Extent(100, 100)).Operations.OfType<RectangleOperation>().Single(operation => operation.Ink == TestInks.Red);

        Assert.Equal(10f, spanning.Size.Height);
    }
}
