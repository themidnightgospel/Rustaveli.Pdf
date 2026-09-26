using Rustaveli.Pdf.Exceptions;

namespace Rustaveli.Pdf.UnitTests;

public class TableDescriptorTests
{
    private static TableElement Compose(Action<TableDescriptor> compose)
    {
        Element root = LayoutHarness.Build(container => container.Table(compose));

        return Assert.IsType<TableElement>(((Container)root).Child);
    }

    private static void TwoColumns(TableDescriptor table) =>
        table.ColumnsDefinition(columns =>
        {
            columns.RelativeColumn();
            columns.RelativeColumn();
        });

    public static TheoryData<string, Action<TableDescriptor>> CallsWithoutAHandler => new()
    {
        { nameof(TableDescriptor.ColumnsDefinition), table => table.ColumnsDefinition(null!) },
        { nameof(TableDescriptor.Header), table => table.Header(null!) },
        { nameof(TableDescriptor.Footer), table => table.Footer(null!) },
    };

    [Theory]
    [MemberData(nameof(CallsWithoutAHandler))]
    public void RefusesAMissingHandler(string method, Action<TableDescriptor> call)
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            call(new TableDescriptor(new TableElement())));

        Assert.True(
            exception.ParamName == "handler",
            $"{method} reported '{exception.ParamName}' as the missing argument.");
    }

    [Fact]
    public void RejectsABodyCellBeyondTheDeclaredColumns()
    {
        DocumentComposeException exception = Assert.Throws<DocumentComposeException>(() => Compose(table =>
        {
            TwoColumns(table);
            table.Cell().Column(3);
        }));

        Assert.Equal(
            "A body cell occupies columns 3 to 3, but the table declares only 2. Add more columns, or reduce the " +
            "cell's column or span.",
            exception.Message);
    }

    [Fact]
    public void RejectsAHeaderCellWhoseSpanOverhangsTheColumns()
    {
        DocumentComposeException exception = Assert.Throws<DocumentComposeException>(() => Compose(table =>
        {
            TwoColumns(table);
            table.Header(header => header.Cell().ColumnSpan(3));
        }));

        Assert.StartsWith("A header cell occupies columns 1 to 3, but the table declares only 2.", exception.Message);
    }

    [Fact]
    public void RejectsAFooterCellWhoseSpanOverhangsTheColumns()
    {
        DocumentComposeException exception = Assert.Throws<DocumentComposeException>(() => Compose(table =>
        {
            TwoColumns(table);
            table.Footer(footer => footer.Cell().Column(2).ColumnSpan(2));
        }));

        Assert.StartsWith("A footer cell occupies columns 2 to 3, but the table declares only 2.", exception.Message);
    }

    [Fact]
    public void AcceptsACellEndingExactlyOnTheLastColumn()
    {
        TableElement table = Compose(descriptor =>
        {
            TwoColumns(descriptor);
            descriptor.Cell().Column(1).ColumnSpan(2);
        });

        TableCell cell = Assert.Single(table.Cells);

        Assert.Equal(1, cell.Column);
        Assert.Equal(2, cell.ColumnSpan);
    }

    [Fact]
    public void ATableWithoutDeclaredColumnsLaysItsCellsOutAsOneColumn()
    {
        TableElement table = Compose(descriptor =>
        {
            descriptor.Cell();
            descriptor.Cell();
        });

        Assert.Equal(new[] { (1, 1), (2, 1) }, table.Cells.Select(cell => (cell.Row, cell.Column)));
    }

    [Fact]
    public void ATableWithoutDeclaredColumnsStillRejectsASecondColumn()
    {
        DocumentComposeException exception = Assert.Throws<DocumentComposeException>(() =>
            Compose(descriptor => descriptor.Cell().Column(2)));

        Assert.StartsWith("A body cell occupies columns 2 to 2, but the table declares only 1.", exception.Message);
    }
}
