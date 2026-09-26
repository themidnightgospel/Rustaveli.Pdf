namespace Rustaveli.Pdf.UnitTests;

public class TableCellDescriptorTests
{
    [Fact]
    public void RejectsARowBeforeTheFirst()
    {
        TableCell cell = new TableCell();

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TableCellDescriptor(cell).Row(0));

        Assert.Equal("row", exception.ParamName);
        Assert.False(cell.HasExplicitRow);
    }

    [Fact]
    public void RejectsAColumnBeforeTheFirst()
    {
        TableCell cell = new TableCell();

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TableCellDescriptor(cell).Column(0));

        Assert.Equal("column", exception.ParamName);
        Assert.False(cell.HasExplicitColumn);
    }

    [Fact]
    public void AcceptsTheFirstRowAndColumnAsExplicitPositions()
    {
        TableCell cell = new TableCell();

        new TableCellDescriptor(cell).Row(1).Column(1);

        Assert.True(cell.HasExplicitRow);
        Assert.True(cell.HasExplicitColumn);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void TreatsASpanBelowOneAsASingleSlot(int span)
    {
        TableCell cell = new TableCell();

        new TableCellDescriptor(cell).RowSpan(span).ColumnSpan(span);

        Assert.Equal(1, cell.RowSpan);
        Assert.Equal(1, cell.ColumnSpan);
    }

    [Fact]
    public void KeepsASpanOfSeveralSlots()
    {
        TableCell cell = new TableCell();

        new TableCellDescriptor(cell).RowSpan(3).ColumnSpan(2);

        Assert.Equal(3, cell.RowSpan);
        Assert.Equal(2, cell.ColumnSpan);
    }
}
