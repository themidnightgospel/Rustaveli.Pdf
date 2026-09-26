using System.Diagnostics;
using Rustaveli.Pdf.Documents;
using Rustaveli.Pdf.Fluent;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Skia;
using UglyToad.PdfPig;
using Xunit.Abstractions;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Timing diagnostics for documents large enough to expose super-linear behaviour.
/// </summary>
/// <remarks>
/// Assertions are deliberately loose — wall-clock limits flake on shared hardware. The value is the printed
/// scaling: if doubling the row count more than doubles the time, the engine is re-measuring work it should be
/// reusing, and that shows up here before it shows up in a bug report.
/// </remarks>
public class PerformanceReportTests(ITestOutputHelper output)
{
    private static byte[] GenerateTable(int rowCount)
    {
        Document document = Document.Create(container => container.Page(page =>
        {
            page.Size = PaperSizes.A4;
            page.Margin = Sides.All(30);

            page.Content().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(80);
                    columns.RelativeColumn();
                    columns.ConstantColumn(60);
                });

                table.Header(header =>
                {
                    header.Cell().Text("Code");
                    header.Cell().Text("Description");
                    header.Cell().Text("Amount");
                });

                for (int index = 0; index < rowCount; index++)
                {
                    table.Cell().Padding(2).Text($"SKU-{index:D4}");
                    table.Cell().Padding(2).Text($"Description for row {index}");
                    table.Cell().Padding(2).Text($"{index * 3.25m:F2}");
                }
            });
        }));

        return document.GeneratePdf();
    }

    private static byte[] GenerateColumn(int itemCount)
    {
        Document document = Document.Create(container => container.Page(page =>
        {
            page.Size = PaperSizes.A4;
            page.Margin = Sides.All(30);

            page.Content().Column(column =>
            {
                column.Spacing(2);

                for (int index = 0; index < itemCount; index++)
                    column.Item().Text($"Paragraph {index}: the quick brown fox jumps over the lazy dog.");
            });
        }));

        return document.GeneratePdf();
    }

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(400)]
    [InlineData(800)]
    [InlineData(1600)]
    [InlineData(3200)]
    public void ReportTableScaling(int rowCount)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        byte[] bytes = GenerateTable(rowCount);
        stopwatch.Stop();

        using PdfDocument parsed = PdfDocument.Open(bytes);

        output.WriteLine(
            $"table rows={rowCount,-5} pages={parsed.NumberOfPages,-4} " +
            $"ms={stopwatch.ElapsedMilliseconds,-6} msPerRow={(double)stopwatch.ElapsedMilliseconds / rowCount:F3}");

        Assert.True(parsed.NumberOfPages > 0);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(400)]
    [InlineData(800)]
    [InlineData(1600)]
    public void ReportColumnScaling(int itemCount)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        byte[] bytes = GenerateColumn(itemCount);
        stopwatch.Stop();

        using PdfDocument parsed = PdfDocument.Open(bytes);

        output.WriteLine(
            $"column items={itemCount,-5} pages={parsed.NumberOfPages,-4} " +
            $"ms={stopwatch.ElapsedMilliseconds,-6} msPerItem={(double)stopwatch.ElapsedMilliseconds / itemCount:F3}");

        Assert.True(parsed.NumberOfPages > 0);
    }
}
