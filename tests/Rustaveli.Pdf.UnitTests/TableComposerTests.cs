namespace Rustaveli.Pdf.UnitTests;

public class TableComposerTests
{
    private static TableBlock Compose(Action<TableComposer> compose)
    {
        Block root = LayoutHarness.Build(frame => frame.Table(compose));

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
        TableBlock table = Compose(composer =>
        {
            TwoColumns(composer);
            composer.Cell().AtColumn(1).SpanColumns(2);
        });

        CellBlock cell = Assert.Single(table.Cells);

        Assert.Equal(1, cell.Column);
        Assert.Equal(2, cell.ColumnSpan);
    }

    public static TheoryData<string, Action<TableComposer>> CellsWithoutColumns => new()
    {
        { "body", table => table.Cell() },
        { "header", table => table.HeaderRows(header => header.Cell()) },
        { "footer", table => table.FooterRows(footer => footer.Cell()) },
    };

    [Theory]
    [MemberData(nameof(CellsWithoutColumns))]
    public void RejectsCellsInATableWithoutDeclaredColumns(string band, Action<TableComposer> cell)
    {
        CompositionException exception = Assert.Throws<CompositionException>(() => Compose(cell));

        Assert.True(
            exception.Message == "A table declares its columns with Columns(...) before its cells can be placed.",
            $"A {band} cell was rejected with: {exception.Message}");
    }

    [Fact]
    public void ATableWithoutColumnsOrCellsIsAccepted()
    {
        TableBlock table = Compose(_ => { });

        Assert.Empty(table.Columns);
    }

    [Fact]
    public void ADocumentWithAColumnlessTableFailsWhileComposing()
    {
        Exception? exception = Record.Exception(() => LayoutHarness.Render(Document.Compose(composition =>
            composition.Section(section => section.Body().Table(table => table.Cell().Text("Cell"))))));

        Assert.IsType<CompositionException>(exception);
    }
}
