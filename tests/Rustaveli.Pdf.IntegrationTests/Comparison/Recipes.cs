using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Rustaveli.Pdf.Fluent;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Skia;

namespace Rustaveli.Pdf.IntegrationTests.Comparison;

public static class Recipes
{
    static Recipes()
    {
        Settings.License = LicenseType.Community;
    }

    public static byte[] RustaveliTextFlow()
    {
        return Rustaveli.Pdf.Documents.Document.Create(delegate(Rustaveli.Pdf.Documents.IComposition container)
        {
            container.Page(delegate(Rustaveli.Pdf.Documents.Section page)
            {
                page.Size = new Rustaveli.Pdf.Primitives.Extent(595f, 842f);
                page.Margin = Sides.All(40f);
                page.DefaultTextStyle = Rustaveli.Pdf.Text.TypeStyle.Default.FontFamilyOf(RecipeData.FontFamily).FontSizeOf(11f);
                page.Content().Column(delegate(Rustaveli.Pdf.Fluent.ColumnDescriptor column)
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
        return Rustaveli.Pdf.Documents.Document.Create(delegate(Rustaveli.Pdf.Documents.IComposition container)
        {
            container.Page(delegate(Rustaveli.Pdf.Documents.Section page)
            {
                page.Size = new Rustaveli.Pdf.Primitives.Extent(595f, 842f);
                page.Margin = Sides.All(40f);
                page.DefaultTextStyle = Rustaveli.Pdf.Text.TypeStyle.Default.FontFamilyOf(RecipeData.FontFamily).FontSizeOf(11f);
                page.Header().Text("Quarterly Statement");
                page.Footer().Text(delegate(Rustaveli.Pdf.Fluent.TextDescriptor text)
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
                page.Content().Column(delegate(Rustaveli.Pdf.Fluent.ColumnDescriptor column)
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
        return Rustaveli.Pdf.Documents.Document.Create(delegate(Rustaveli.Pdf.Documents.IComposition container)
        {
            container.Page(delegate(Rustaveli.Pdf.Documents.Section page)
            {
                page.Size = new Rustaveli.Pdf.Primitives.Extent(595f, 842f);
                page.Margin = Sides.All(40f);
                page.DefaultTextStyle = Rustaveli.Pdf.Text.TypeStyle.Default.FontFamilyOf(RecipeData.FontFamily).FontSizeOf(11f);
                page.Content().Table(delegate(Rustaveli.Pdf.Fluent.TableDescriptor table)
                {
                    table.ColumnsDefinition(delegate(Rustaveli.Pdf.Fluent.TableColumnsDefinitionDescriptor columns)
                    {
                        columns.ConstantColumn(90f);
                        columns.RelativeColumn();
                        columns.ConstantColumn(70f);
                    });
                    table.Header(delegate(TableBandDescriptor header)
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
