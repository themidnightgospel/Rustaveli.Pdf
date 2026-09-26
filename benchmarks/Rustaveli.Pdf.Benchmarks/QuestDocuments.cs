using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Rustaveli.Pdf.Benchmarks;

/// <summary>
/// The benchmark documents, built with the QuestPDF oracle. Mirrors <see cref="RustaveliDocuments" /> element for
/// element; a difference between the two is a benchmark bug.
/// </summary>
public static class QuestDocuments
{
    public static byte[] Generate(DocumentKind kind)
    {
        BenchmarkFonts.EnsureRegistered();

        List<Image> images = new List<Image>();
        try
        {
            return Build(kind, images).GeneratePdf();
        }
        finally
        {
            foreach (Image image in images)
                image.Dispose();
        }
    }

    private static Document Build(DocumentKind kind, List<Image> images) => Document.Create(container => container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(40f);
        page.DefaultTextStyle(style => style.FontFamily(BenchmarkFonts.Family).FontSize(10f));

        page.Header().PaddingBottom(10f).Text(kind.ToString());
        page.Footer().AlignCenter().Text(text =>
        {
            text.Span("Page ");
            text.CurrentPageNumber();
            text.Span(" of ");
            text.TotalPages();
        });

        switch (kind)
        {
            case DocumentKind.Invoice:
                page.Content().Column(column =>
                {
                    column.Spacing(12f);
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Invoice INV-2026-0042\nIssued 26 September 2026");
                        row.ConstantItem(160f).Text("Customer\nRustaveli Avenue 1\nTbilisi");
                    });
                    column.Item().Table(table => ItemTable(table, BenchmarkData.InvoiceLines));
                    column.Item().AlignRight().Text("Total due 12 345.67");
                });
                break;

            case DocumentKind.Report:
                page.Content().Column(column =>
                {
                    column.Spacing(6f);
                    foreach (string paragraph in BenchmarkData.ReportParagraphs)
                        column.Item().Text(paragraph);
                });
                break;

            case DocumentKind.LargeTable:
                page.Content().Table(table => ItemTable(table, BenchmarkData.TableRows));
                break;

            case DocumentKind.Images:
                page.Content().Column(column =>
                {
                    column.Spacing(8f);
                    for (int index = 0; index < 40; index++)
                    {
                        Image image = Image.FromBinaryData(BenchmarkData.Photographs[index % BenchmarkData.Photographs.Count]);
                        images.Add(image);
                        column.Item().Image(image).FitWidth();
                        column.Item().Text($"Figure {index + 1}");
                    }
                });
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }));

    private static void ItemTable(TableDescriptor table, IReadOnlyList<LineItem> lines)
    {
        table.ColumnsDefinition(columns =>
        {
            columns.ConstantColumn(80f);
            columns.RelativeColumn();
            columns.ConstantColumn(80f);
        });

        table.Header(header =>
        {
            header.Cell().Text("Code");
            header.Cell().Text("Description");
            header.Cell().AlignRight().Text("Amount");
        });

        foreach (LineItem line in lines)
        {
            table.Cell().Text(line.Code);
            table.Cell().Text(line.Description);
            table.Cell().AlignRight().Text(line.Amount);
        }
    }
}
