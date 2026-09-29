namespace Rustaveli.Pdf.UnitTests;

public class CellFrameTests
{
    [Fact]
    public void RejectsARowBeforeTheFirst()
    {
        CellBlock cell = new CellBlock();

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CellFrame(cell).AtRow(0));

        Assert.Equal("row", exception.ParamName);
        Assert.False(cell.HasExplicitRow);
    }

    [Fact]
    public void RejectsAColumnBeforeTheFirst()
    {
        CellBlock cell = new CellBlock();

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CellFrame(cell).AtColumn(0));

        Assert.Equal("column", exception.ParamName);
        Assert.False(cell.HasExplicitColumn);
    }

    [Fact]
    public void AcceptsTheFirstRowAndColumnAsExplicitPositions()
    {
        CellBlock cell = new CellBlock();

        new CellFrame(cell).AtRow(1).AtColumn(1);

        Assert.True(cell.HasExplicitRow);
        Assert.True(cell.HasExplicitColumn);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void RejectsASpanBelowOne(int span)
    {
        CellFrame cell = new CellFrame(new CellBlock());

        Assert.Equal("span", Assert.Throws<ArgumentOutOfRangeException>(() => cell.SpanRows(span)).ParamName);
        Assert.Equal("span", Assert.Throws<ArgumentOutOfRangeException>(() => cell.SpanColumns(span)).ParamName);
    }

    [Fact]
    public void KeepsASpanOfSeveralSlots()
    {
        CellBlock cell = new CellBlock();

        new CellFrame(cell).SpanRows(3).SpanColumns(2);

        Assert.Equal(3, cell.RowSpan);
        Assert.Equal(2, cell.ColumnSpan);
    }
}
