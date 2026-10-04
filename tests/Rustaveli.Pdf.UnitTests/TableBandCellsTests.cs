namespace Rustaveli.Pdf.UnitTests;

/// <summary>
/// Finding the cells of a band that start in the rows a page draws: the places in the band's list, in list order,
/// whether or not the list is in row order.
/// </summary>
public class TableBandCellsTests
{
    private static TableBlock.BandCells Band(params int[] rows) =>
        new TableBlock.BandCells([.. rows.Select(row => new CellBlock { Row = row })]);

    [Fact]
    public void FindsTheCellsStartingInARangeOfRowsOfABandInRowOrder()
    {
        TableBlock.BandCells band = Band(1, 1, 2, 3, 3, 3, 5);

        Assert.Equal([0, 1], band.Starting(1, 1).ToArray());
        Assert.Equal([2, 3, 4, 5], band.Starting(2, 3).ToArray());
        Assert.Equal([2, 3, 4, 5, 6], band.Starting(2, 5).ToArray());
        Assert.Empty(band.Starting(4, 4).ToArray());
        Assert.Equal([6], band.Starting(4, 9).ToArray());
        Assert.Empty(band.Starting(6, 9).ToArray());
    }

    [Fact]
    public void FindsTheCellsStartingInARangeOfRowsOfABandOutOfRowOrderInListOrder()
    {
        // Cells pinned to rows can be listed in any order; a page still draws them in the order they were listed.
        TableBlock.BandCells band = Band(2, 1, 3, 1, 2);

        Assert.Equal([1, 3], band.Starting(1, 1).ToArray());
        Assert.Equal([0, 1, 3, 4], band.Starting(1, 2).ToArray());
        Assert.Equal([0, 2, 4], band.Starting(2, 3).ToArray());
        Assert.Empty(band.Starting(4, 9).ToArray());
    }

    [Fact]
    public void AnEmptyBandHasNoCellsInAnyRows()
    {
        TableBlock.BandCells band = Band();

        Assert.Empty(band.Starting(1, 1).ToArray());
        Assert.Empty(band.Starting(1, 100).ToArray());
    }

    [Fact]
    public void CellsSharingAStartingRowComeInListOrder()
    {
        TableBlock.BandCells band = Band(1, 2, 2, 2, 3);
        int[] places = band.Starting(2, 2).ToArray();

        Assert.Equal([1, 2, 3], places);
        Assert.Equal(places.Length, band.Starting(2, 2).Count);
        Assert.Equal(2, band.Starting(2, 2)[1]);
    }

#if NET
    [Fact]
    public void FindingThePagesCellsOfABandInRowOrderAllocatesAlmostNothing()
    {
        // Allocation budget: a band listed in row order, as a table's body almost always is, finds a page's cells as
        // one run of its list. Indexing every row and gathering each page's cells into a sorted list cost a long
        // table about 3 MB.
        const long Budget = 256;
        List<CellBlock> cells = [.. Enumerable.Range(0, 3_000).Select(index => new CellBlock { Row = index / 3 + 1 })];
        new TableBlock.BandCells(cells).Starting(1, 50);

        long before = GC.GetAllocatedBytesForCurrentThread();
        TableBlock.BandCells band = new TableBlock.BandCells(cells);
        int found = 0;

        for (int firstRow = 1; firstRow <= 1_000; firstRow += 50)
            found += band.Starting(firstRow, firstRow + 49).Count;

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(3_000, found);
        Assert.True(allocated <= Budget, $"Indexing the band and finding its pages' cells allocated {allocated:N0} bytes; its budget is {Budget:N0}.");
    }
#endif
}
