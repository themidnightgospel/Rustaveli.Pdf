using Rustaveli.Pdf.Documents;
using Rustaveli.Pdf.Fluent;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Skia;
using Rustaveli.Pdf.Text;

namespace Rustaveli.Pdf.Benchmarks;

/// <summary>The benchmark documents, built with this library. Mirrors <see cref="QuestDocuments" /> element for element.</summary>
public static class RustaveliDocuments
{
    public static byte[] Generate(DocumentKind kind)
    {
        BenchmarkFonts.EnsureRegistered();

        List<SkiaImage> images = new List<SkiaImage>();
        try
        {
            return Build(kind, images).ExportPdf();
        }
        finally
        {
            foreach (SkiaImage image in images)
                image.Dispose();
        }
    }

    private static Document Build(DocumentKind kind, List<SkiaImage> images) => Document.Compose(container => container.Section(page =>
    {
        page.Trim = PaperSizes.A4;
        page.Margins = Sides.All(40f);
        page.DefaultType = TypeStyle.Default.WithTypeface(BenchmarkFonts.Family).WithPointSize(10f);

        page.RunningHead().InsetBottom(10f).Text(kind.ToString());
        page.RunningFoot().Centered().Text(text =>
        {
            text.Run("Page ");
            text.Folio();
            text.Run(" of ");
            text.PageCount();
        });

        switch (kind)
        {
            case DocumentKind.Invoice:
                page.Body().Stack(column =>
                {
                    column.SpaceBetween(12f);
                    column.Add().Columns(row =>
                    {
                        row.Share().Text("Invoice INV-2026-0042\nIssued 26 September 2026");
                        row.Fixed(160f).Text("Customer\nRustaveli Avenue 1\nTbilisi");
                    });
                    column.Add().Table(table => ItemTable(table, BenchmarkData.InvoiceLines));
                    column.Add().FlushRight().Text("Total due 12 345.67");
                });
                break;

            case DocumentKind.Report:
                page.Body().Stack(column =>
                {
                    column.SpaceBetween(6f);
                    foreach (string paragraph in BenchmarkData.ReportParagraphs)
                        column.Add().Text(paragraph);
                });
                break;

            case DocumentKind.LargeTable:
                page.Body().Table(table => ItemTable(table, BenchmarkData.TableRows));
                break;

            case DocumentKind.Images:
                page.Body().Stack(column =>
                {
                    column.SpaceBetween(8f);
                    for (int index = 0; index < 40; index++)
                    {
                        SkiaImage image = SkiaImage.FromBytes(BenchmarkData.Photographs[index % BenchmarkData.Photographs.Count]);
                        images.Add(image);
                        column.Add().Image(image, ImageFitting.FitWidth);
                        column.Add().Text($"Figure {index + 1}");
                    }
                });
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }));

    private static void ItemTable(TableComposer table, IReadOnlyList<LineItem> lines)
    {
        table.Columns(columns =>
        {
            columns.Fixed(80f);
            columns.Share();
            columns.Fixed(80f);
        });

        table.HeaderRows(header =>
        {
            header.Cell().Text("Code");
            header.Cell().Text("Description");
            header.Cell().FlushRight().Text("Amount");
        });

        foreach (LineItem line in lines)
        {
            table.Cell().Text(line.Code);
            table.Cell().Text(line.Description);
            table.Cell().FlushRight().Text(line.Amount);
        }
    }
}
