using System.Diagnostics;
using Rustaveli.Pdf.Writing;
using UglyToad.PdfPig;
using Xunit.Abstractions;

namespace Rustaveli.Pdf.IntegrationTests.Writing;

/// <summary>
/// Timing for the managed writer on a long document. The bound is deliberately loose — shared CI machines are
/// noisy — and the printed figures are the point: a writer that should take well under a second creeping towards
/// the limit shows up here long before it would in a benchmark run.
/// </summary>
public class WriterPerformanceTests(ITestOutputHelper output)
{
    private const int PageCount = 10_000;

    private static byte[] Write(string format, int pageCount)
    {
        PdfWriterOptions options = new PdfWriterOptions
        {
            CrossReferenceFormat = (PdfCrossReferenceFormat)Enum.Parse(typeof(PdfCrossReferenceFormat), format),
        };

        using MemoryStream stream = new MemoryStream();
        using (PdfDocumentWriter document = new PdfDocumentWriter(stream, options))
        {
            PdfReference translucent = document.GetOpacityState(0.5);
            for (int index = 0; index < pageCount; index++)
            {
                PdfPage page = document.BeginPage(595.28f, 841.89f);
                ContentStreamBuilder content = page.Content;

                content.SaveState();
                content.SetFillRgb(0.9f, 0.9f, 0.95f);
                content.Rectangle(36, 36, 523.28f, 769.89f);
                content.Fill();
                content.SetStrokeRgb(0.2f, 0.2f, 0.2f);
                content.SetLineWidth(0.75f);
                for (int row = 0; row < 10; row++)
                {
                    content.MoveTo(36, 100 + (row * 60.5f));
                    content.LineTo(559.28f, 100 + (row * 60.5f));
                }

                content.Stroke();
                content.SetGraphicsState(page.Resources.GetExtGStateName(translucent));
                content.SetFillCmyk(0, 0.5f, 1, 0);
                content.Rectangle(400, 700, 120.25f, 80.5f);
                content.Fill();
                content.RestoreState();

                page.AddDestinationLink(new PdfRectangle(36, 36, 136, 56), $"page-{(index + 1) % pageCount}");
                document.AddNamedDestination($"page-{index}", page.Reference, 0, 841.89f);
                document.EndPage(page);
            }

            document.Info.Title = "Performance";
            document.Finish();
        }

        return stream.ToArray();
    }

    [Theory]
    [InlineData("Stream")]
    [InlineData("Table")]
    public void WritesTenThousandPagesQuickly(string format)
    {
        // Warm up first, so the measurement is the writer and not the JIT.
        Write(format, 50);

        Stopwatch stopwatch = Stopwatch.StartNew();
        byte[] pdf = Write(format, PageCount);
        stopwatch.Stop();

        output.WriteLine(
            $"{format}: {PageCount} pages in {stopwatch.ElapsedMilliseconds} ms " +
            $"({PageCount / stopwatch.Elapsed.TotalSeconds:F0} pages/s), {pdf.Length / 1024} KiB, {pdf.Length / PageCount} bytes/page");

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Writing {PageCount} pages took {stopwatch.Elapsed}.");
        using PdfDocument document = PdfDocument.Open(pdf);
        Assert.Equal(PageCount, document.NumberOfPages);
    }
}
