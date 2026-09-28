using Rustaveli.Pdf.ConformanceTests.Validation;

namespace Rustaveli.Pdf.ConformanceTests.Guide;

/// <summary>The examples of docs/guide/output.md, as written there.</summary>
[Collection(WorkingDirectoryCollection.Name)]
public class OutputTests
{
    private static Document TwoPages() => Document.Compose(composition => composition.Section(section =>
    {
        section.Trim = PaperSizes.A6;
        section.Body().Stack(stack =>
        {
            stack.Add().Text("One");
            stack.Add().NewPage();
            stack.Add().Text("Two");
        });
    }));

    [Fact]
    public void PageImagesSvgAndXps()
    {
        using TemporaryWorkingDirectory directory = new TemporaryWorkingDirectory();
        Document document = TwoPages();

        IReadOnlyList<byte[]> pages = document.ExportImages(new ImageExportOptions
        {
            Resolution = 150,
            Format = PageImageFormat.Jpeg,
            Quality = 85,
        });

        document.ExportImages(page => $"page-{page}.png");

        IReadOnlyList<string> svgPages = document.ExportSvg();

        if (OperatingSystem.IsWindows())
        {
            document.ExportXps("document.xps");
            Assert.True(new FileInfo("document.xps").Length > 0);
        }

        Assert.Equal(2, pages.Count);
        Assert.All(pages, page => Assert.Equal([0xFF, 0xD8], page.Take(2)));
        Assert.True(File.Exists("page-1.png") && File.Exists("page-2.png"));
        Assert.Equal(2, svgPages.Count);
        Assert.All(svgPages, svg => Assert.Contains("<svg", svg, StringComparison.Ordinal));
    }

    [Fact]
    public void PdfAAndPdfUA()
    {
        Document invoice = TwoPages();
        byte[] archived = Archive(invoice);

        Artwork chart = Artwork.Draw(200, 100, draw =>
            draw.Fill(new VectorPath().AddRectangle(0, 40, 60, 60), Ink.Hex("#1E88E5")));

        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = PaperSizes.A5;
            section.Margins = Sides.All(36);
            section.RunningFoot().Centered().Text(text => text.Folio());

            section.Body().Stack(stack =>
            {
                stack.SpaceBetween(8);
                stack.Add().Tagged(ContentTag.Heading(1)).Text(text => text.Run("Annual report").PointSize(18));
                stack.Add().Text("Paragraphs are tagged as paragraphs without asking.");
                stack.Add().Tagged(ContentTag.Figure("Revenue rose in every quarter")).Width(200).Artwork(chart);
                stack.Add().Untagged().Rule(0.5f);
            });
        }));

        document.Info.Title = "Annual report";
        document.Info.Language = "en";

        byte[] accessible = document.ExportPdf(new PdfExportOptions { Accessibility = PdfUAConformance.PdfUA1 });

        IReadOnlyDictionary<string, IReadOnlyList<string>> broken = VeraPdf.Validate(new Dictionary<string, byte[]>
        {
            ["archived"] = archived,
            ["accessible"] = accessible,
        });

        Assert.True(broken.Values.All(rules => rules.Count == 0), "veraPDF found:\n" + string.Join("\n", broken.SelectMany(file => file.Value.Select(rule => file.Key + ": " + rule))));
    }

    [Fact]
    public void Protection()
    {
        Document document = TwoPages();

        byte[] locked = document.ExportPdf(new PdfExportOptions
        {
            Protection = new Protection
            {
                UserPassword = "open sesame",
                OwnerPassword = "keeper",
                AllowCopying = false,
                AllowModifying = false,
            },
        });

        Assert.Throws<IncorrectPasswordException>(() => PdfFile.Open(locked));
        Assert.Equal(2, PdfFile.Open(locked, "open sesame").PageCount);
        Assert.Equal(2, PdfFile.Open(locked, "keeper").PageCount);
    }

    private static byte[] Archive(Document document)
    {
        document.Info.Title = "Invoice 2026-041";

        byte[] archived = document.ExportPdf(new PdfExportOptions { Conformance = PdfAConformance.PdfA3B });
        return archived;
    }
}
