using System.IO.Compression;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.ConformanceTests.Writing;

/// <summary>Complete documents written directly with the managed writer, exercising every feature that needs no font.</summary>
internal static class WriterSamples
{
    public static PdfWriterOptions Options(string format, CompressionLevel level = CompressionLevel.Optimal) =>
        new PdfWriterOptions
        {
            CrossReferenceFormat = (PdfCrossReferenceFormat)Enum.Parse(typeof(PdfCrossReferenceFormat), format),
            CompressionLevel = level,
        };

    public static byte[] Write(PdfWriterOptions options, Action<PdfDocumentWriter> build)
    {
        using MemoryStream output = new MemoryStream();
        using (PdfDocumentWriter document = new PdfDocumentWriter(output, options))
        {
            build(document);
            document.Finish();
        }

        return output.ToArray();
    }

    /// <summary>Three pages of shapes, colours, transparency, a spot colour, a form XObject, links and destinations.</summary>
    public static byte[] Showcase(PdfWriterOptions options) => Write(options, document =>
    {
        document.Info.Title = "Writer showcase — ვეფხისტყაოსანი";
        document.Info.Author = "Rustaveli.Pdf";
        document.Info.Subject = "Conformance";
        document.Info.Keywords = "pdf, writer";
        document.Info.Creator = "Conformance tests";
        document.Info.Producer = "Rustaveli.Pdf";
        document.Info.CreationDate = new DateTimeOffset(2026, 9, 26, 10, 30, 0, TimeSpan.FromHours(4));
        document.Info.ModificationDate = new DateTimeOffset(2026, 9, 26, 11, 45, 0, TimeSpan.FromMinutes(-150));

        PdfReference spot = document.File.Write(new PdfArray
        {
            new PdfName("Separation"),
            new PdfName("Spot Red"),
            new PdfName("DeviceCMYK"),
            new PdfDictionary
            {
                [new PdfName("FunctionType")] = 2,
                [new PdfName("Domain")] = new PdfArray { 0, 1 },
                [new PdfName("C0")] = new PdfArray { 0, 0, 0, 0 },
                [new PdfName("C1")] = new PdfArray { 0, 1, 1, 0 },
                [PdfNames.N] = 1,
            },
        });

        PdfReference badge = FormXObject(document);

        PdfPage first = document.BeginPage(595.28, 841.89);
        Shapes(document, first, spot, badge);
        first.AddUriLink(new PdfRectangle(50, 700, 250, 720), "https://example.com/rustaveli?q=ვეფხი");
        first.AddDestinationLink(new PdfRectangle(50, 650, 250, 670), "appendix");
        document.AddNamedDestination("start", first.Reference, 0, 841.89);
        document.EndPage(first);

        PdfPage second = document.BeginPage(612, 792);
        second.Content.SetStrokeCmyk(1, 0, 0, 0);
        second.Content.SetLineWidth(2);
        second.Content.MoveTo(72, 72);
        second.Content.LineTo(540, 720);
        second.Content.Stroke();
        second.AddDestinationLink(new PdfRectangle(72, 72, 172, 92), "start");
        document.EndPage(second);

        PdfPage third = document.BeginPage(new PdfRectangle(0, 0, 400, 300));
        third.Entries[new PdfName("Rotate")] = 90;
        document.AddNamedDestination("appendix", third.Reference, 20, 280);
        document.EndPage(third);
    });

    /// <summary>A long document: one rectangle, one link and one named destination on every page.</summary>
    public static byte[] ManyPages(PdfWriterOptions options, int pageCount) => Write(options, document =>
    {
        for (int index = 0; index < pageCount; index++)
        {
            PdfPage page = document.BeginPage(300, 400);
            page.Content.SetFillRgb(index % 3 / 2.0, 0.5, 1 - (index % 5 / 4.0));
            page.Content.Rectangle(20, 20, 260, 360);
            page.Content.Fill();
            page.AddDestinationLink(new PdfRectangle(20, 20, 120, 40), $"page-{(index + 1) % pageCount}");
            document.AddNamedDestination($"page-{index}", page.Reference, 0, 400);
            document.EndPage(page);
        }
    });

    private static PdfReference FormXObject(PdfDocumentWriter document)
    {
        using ContentStreamBuilder content = new ContentStreamBuilder();
        content.SetFillRgb(0.9, 0.7, 0.1);
        content.MoveTo(0, 0);
        content.CurveTo(0, 30, 60, 30, 60, 0);
        content.ClosePath();
        content.Fill();

        return document.File.WriteStream(
            new PdfDictionary
            {
                [PdfNames.Type] = PdfNames.XObject,
                [PdfNames.Subtype] = new PdfName("Form"),
                [new PdfName("BBox")] = new PdfArray { 0, 0, 60, 30 },
                [PdfNames.Resources] = new PdfDictionary(),
            },
            content.Content);
    }

    private static void Shapes(PdfDocumentWriter document, PdfPage page, PdfReference spot, PdfReference badge)
    {
        ContentStreamBuilder content = page.Content;

        content.SaveState();
        content.SetFillRgb(0.2, 0.4, 0.8);
        content.Rectangle(50, 50, 200, 100);
        content.Fill();
        content.RestoreState();

        content.SaveState();
        content.SetStrokeGray(0.25);
        content.SetLineWidth(3);
        content.SetLineCap(PdfLineCap.Round);
        content.SetLineJoin(PdfLineJoin.Bevel);
        content.SetMiterLimit(5);
        content.SetDashPattern(new[] { 6.0, 3 }, 1);
        content.MoveTo(300, 50);
        content.LineTo(500, 150);
        content.CurveTo(520, 200, 480, 250, 450, 300);
        content.Stroke();
        content.RestoreState();

        content.SaveState();
        content.SetGraphicsState(page.Resources.GetExtGStateName(document.GetOpacityState(0.5)));
        content.SetFillCmyk(0, 0.8, 0.9, 0);
        content.SetStrokeRgb(0, 0, 0);
        content.Rectangle(100, 200, 150, 150);
        content.FillAndStroke();
        content.RestoreState();

        content.SaveState();
        content.Rectangle(300, 350, 100, 100);
        content.Clip();
        content.EndPath();
        content.SetFillColorSpace(page.Resources.GetColorSpaceName(spot));
        content.SetFillColorN(new[] { 0.75 });
        content.Rectangle(280, 330, 150, 150);
        content.FillEvenOdd();
        content.RestoreState();

        content.SaveState();
        content.Transform(2, 0, 0, 2, 100, 450);
        content.PaintXObject(page.Resources.GetXObjectName(badge));
        content.RestoreState();

        content.SaveState();
        content.Rectangle(400, 500, 50, 50);
        content.ClipEvenOdd();
        content.EndPath();
        content.SetFillGray(0.1);
        content.Rectangle(400, 500, 50, 50);
        content.Fill();
        content.RestoreState();
    }
}
