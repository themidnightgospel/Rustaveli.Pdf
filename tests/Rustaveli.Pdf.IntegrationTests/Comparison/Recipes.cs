using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Rustaveli.Pdf.IntegrationTests.Comparison;

public static class Recipes
{
    static Recipes()
    {
        Settings.License = LicenseType.Community;
    }

    public static byte[] RustaveliTextFlow()
    {
        return Rustaveli.Pdf.Document.Compose(delegate(Rustaveli.Pdf.IComposition container)
        {
            container.Section(delegate(Rustaveli.Pdf.Section page)
            {
                page.Trim = new Rustaveli.Pdf.Extent(595f, 842f);
                page.Margins = Sides.All(40f);
                page.DefaultType = Rustaveli.Pdf.TypeStyle.Default.WithTypeface(RecipeData.FontFamily).WithPointSize(11f);
                page.Body().Stack(delegate(Rustaveli.Pdf.StackComposer column)
                {
                    column.SpaceBetween(8f);
                    foreach (string paragraph in RecipeData.Paragraphs)
                    {
                        column.Add().Text(paragraph);
                    }
                });
            });
        }).ExportPdf();
    }

    public static byte[] QuestTextFlow()
    {
        return QuestPDF.Fluent.Document.Create(delegate(QuestPDF.Infrastructure.IDocumentContainer container)
        {
            container.Page(delegate(QuestPDF.Fluent.PageDescriptor page)
            {
                page.Size(595f, 842f);
                page.Margin(40f);
                page.DefaultTextStyle((QuestPDF.Infrastructure.TextStyle style) => style.FontFamily(RecipeData.FontFamily).FontSize(11f));
                page.Content().Column(delegate(QuestPDF.Fluent.ColumnDescriptor column)
                {
                    column.Spacing(8f);
                    foreach (string paragraph in RecipeData.Paragraphs)
                    {
                        column.Item().Text(paragraph);
                    }
                });
            });
        }).GeneratePdf();
    }

    public static byte[] RustaveliPaginated()
    {
        return Rustaveli.Pdf.Document.Compose(delegate(Rustaveli.Pdf.IComposition container)
        {
            container.Section(delegate(Rustaveli.Pdf.Section page)
            {
                page.Trim = new Rustaveli.Pdf.Extent(595f, 842f);
                page.Margins = Sides.All(40f);
                page.DefaultType = Rustaveli.Pdf.TypeStyle.Default.WithTypeface(RecipeData.FontFamily).WithPointSize(11f);
                page.RunningHead().Text("Quarterly Statement");
                page.RunningFoot().Text(delegate(Rustaveli.Pdf.TextComposer text)
                {
                    text.Run("Page ");
                    text.Folio();
                    text.Run(" of ");
                    text.PageCount();
                });
                page.Body().Stack(delegate(Rustaveli.Pdf.StackComposer column)
                {
                    column.SpaceBetween(4f);
                    foreach ((string Code, string Description, string Amount) row in RecipeData.Rows)
                    {
                        column.Add().Text($"{row.Code} {row.Description} {row.Amount}");
                    }
                });
            });
        }).ExportPdf();
    }

    public static byte[] QuestPaginated()
    {
        return QuestPDF.Fluent.Document.Create(delegate(QuestPDF.Infrastructure.IDocumentContainer container)
        {
            container.Page(delegate(QuestPDF.Fluent.PageDescriptor page)
            {
                page.Size(595f, 842f);
                page.Margin(40f);
                page.DefaultTextStyle((QuestPDF.Infrastructure.TextStyle style) => style.FontFamily(RecipeData.FontFamily).FontSize(11f));
                page.Header().Text("Quarterly Statement");
                page.Footer().Text(delegate(QuestPDF.Fluent.TextDescriptor text)
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
                page.Content().Column(delegate(QuestPDF.Fluent.ColumnDescriptor column)
                {
                    column.Spacing(4f);
                    foreach ((string Code, string Description, string Amount) row in RecipeData.Rows)
                    {
                        column.Item().Text($"{row.Code} {row.Description} {row.Amount}");
                    }
                });
            });
        }).GeneratePdf();
    }

    public static byte[] RustaveliTable()
    {
        return Rustaveli.Pdf.Document.Compose(delegate(Rustaveli.Pdf.IComposition container)
        {
            container.Section(delegate(Rustaveli.Pdf.Section page)
            {
                page.Trim = new Rustaveli.Pdf.Extent(595f, 842f);
                page.Margins = Sides.All(40f);
                page.DefaultType = Rustaveli.Pdf.TypeStyle.Default.WithTypeface(RecipeData.FontFamily).WithPointSize(11f);
                page.Body().Table(delegate(Rustaveli.Pdf.TableComposer table)
                {
                    table.Columns(delegate(Rustaveli.Pdf.TableColumns columns)
                    {
                        columns.Fixed(90f);
                        columns.Share();
                        columns.Fixed(70f);
                    });
                    table.HeaderRows(delegate(TableBand header)
                    {
                        header.Cell().Text("Code");
                        header.Cell().Text("Description");
                        header.Cell().Text("Amount");
                    });
                    foreach ((string Code, string Description, string Amount) row in RecipeData.Rows)
                    {
                        table.Cell().Text(row.Code);
                        table.Cell().Text(row.Description);
                        table.Cell().Text(row.Amount);
                    }
                });
            });
        }).ExportPdf();
    }

    public static byte[] QuestTable()
    {
        return QuestPDF.Fluent.Document.Create(delegate(QuestPDF.Infrastructure.IDocumentContainer container)
        {
            container.Page(delegate(QuestPDF.Fluent.PageDescriptor page)
            {
                page.Size(595f, 842f);
                page.Margin(40f);
                page.DefaultTextStyle((QuestPDF.Infrastructure.TextStyle style) => style.FontFamily(RecipeData.FontFamily).FontSize(11f));
                page.Content().Table(delegate(QuestPDF.Fluent.TableDescriptor table)
                {
                    table.ColumnsDefinition(delegate(QuestPDF.Fluent.TableColumnsDefinitionDescriptor columns)
                    {
                        columns.ConstantColumn(90f);
                        columns.RelativeColumn();
                        columns.ConstantColumn(70f);
                    });
                    table.Header(delegate(QuestPDF.Fluent.TableCellDescriptor header)
                    {
                        header.Cell().Text("Code");
                        header.Cell().Text("Description");
                        header.Cell().Text("Amount");
                    });
                    foreach ((string Code, string Description, string Amount) row in RecipeData.Rows)
                    {
                        table.Cell().Text(row.Code);
                        table.Cell().Text(row.Description);
                        table.Cell().Text(row.Amount);
                    }
                });
            });
        }).GeneratePdf();
    }
}
