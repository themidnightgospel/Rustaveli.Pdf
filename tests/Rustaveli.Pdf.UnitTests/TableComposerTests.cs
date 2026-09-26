using Rustaveli.Pdf.Exceptions;

namespace Rustaveli.Pdf.UnitTests;

public class TableComposerTests
{
    private static TableBlock Compose(Action<TableComposer> compose)
    {
        Block root = LayoutHarness.Build(container => container.Table(compose));

        return Assert.IsType<TableBlock>(((Frame)root).Child);
    }

    private static void TwoColumns(TableComposer table) =>
        table.Columns(columns =>
        {
            columns.Share();
            columns.Share();
        });

    public static TheoryData<string, Action<TableComposer>> CallsWithoutAHandler => new()
    {
        { nameof(TableComposer.Columns), table => table.Columns(null!) },
        { nameof(TableComposer.HeaderRows), table => table.HeaderRows(null!) },
        { nameof(TableComposer.FooterRows), table => table.FooterRows(null!) },
    };

    [Theory]
    [MemberData(nameof(CallsWithoutAHandler))]
    public void RefusesAMissingHandler(string method, Action<TableComposer> call)
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            call(new TableComposer(new TableBlock())));

        Assert.True(
            exception.ParamName == "handler",
            $"{method} reported '{exception.ParamName}' as the missing argument.");
    }

    [Fact]
    public void RejectsABodyCellBeyondTheDeclaredColumns()
    {
        CompositionException exception = Assert.Throws<CompositionException>(() => Compose(table =>
        {
            TwoColumns(table);
            table.Cell().AtColumn(3);
        }));

        Assert.Equal(
            "A body cell occupies columns 3 to 3, but the table declares only 2. Add more columns, or reduce the " +
            "cell's column or span.",
            exception.Message);
    }

    [Fact]
    public void RejectsAHeaderCellWhoseSpanOverhangsTheColumns()
    {
        CompositionException exception = Assert.Throws<CompositionException>(() => Compose(table =>
        {
            TwoColumns(table);
            table.HeaderRows(header => header.Cell().SpanColumns(3));
        }));

        Assert.StartsWith("A header cell occupies columns 1 to 3, but the table declares only 2.", exception.Message);
    }

    [Fact]
    public void RejectsAFooterCellWhoseSpanOverhangsTheColumns()
    {
        CompositionException exception = Assert.Throws<CompositionException>(() => Compose(table =>
        {
            TwoColumns(table);
            table.FooterRows(footer => footer.Cell().AtColumn(2).SpanColumns(2));
        }));

        Assert.StartsWith("A footer cell occupies columns 2 to 3, but the table declares only 2.", exception.Message);
    }

    [Fact]
    public void AcceptsACellEndingExactlyOnTheLastColumn()
    {
        TableBlock table = Compose(descriptor =>
        {
            TwoColumns(descriptor);
            descriptor.Cell().AtColumn(1).SpanColumns(2);
        });

        CellBlock cell = Assert.Single(table.Cells);

        Assert.Equal(1, cell.Column);
        Assert.Equal(2, cell.ColumnSpan);
    }

    [Fact]
    public void ATableWithoutDeclaredColumnsLaysItsCellsOutAsOneColumn()
    {
        TableBlock table = Compose(descriptor =>
        {
            descriptor.Cell();
            descriptor.Cell();
        });

        Assert.Equal(new[] { (1, 1), (2, 1) }, table.Cells.Select(cell => (cell.Row, cell.Column)));
    }

    [Fact]
    public void ATableWithoutDeclaredColumnsStillRejectsASecondColumn()
    {
        CompositionException exception = Assert.Throws<CompositionException>(() =>
            Compose(descriptor => descriptor.Cell().AtColumn(2)));

        Assert.StartsWith("A body cell occupies columns 2 to 2, but the table declares only 1.", exception.Message);
    }
}
