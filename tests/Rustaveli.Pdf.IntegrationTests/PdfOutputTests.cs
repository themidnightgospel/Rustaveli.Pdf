using Rustaveli.Pdf.Documents;
using Rustaveli.Pdf.Elements;
using Rustaveli.Pdf.Fluent;
using Rustaveli.Pdf.Primitives;
using Rustaveli.Pdf.Skia;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Annotations;
using UglyToad.PdfPig.Content;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// End-to-end checks that the Skia backend emits PDFs a third-party reader can parse.
/// </summary>
/// <remarks>
/// The unit tests verify layout against a recording canvas; these verify that what the layout engine decides
/// actually survives serialisation into a real file. PdfPig is used as an independent reader so the assertions
/// do not depend on our own writing code being correct.
/// </remarks>
public class PdfOutputTests
{
    private static Document SimpleDocument(Action<Section> configure) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size = PaperSizes.A4;
            page.Margin = Sides.All(30);
            configure(page);
        }));

    [Fact]
    public void ProducesAFileWithAPdfHeader()
    {
        Document document = SimpleDocument(page => page.Content().Text("Hello, world."));

        byte[] bytes = document.GeneratePdf();

        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF"u8.ToArray(), bytes.Take(4).ToArray());
    }

    [Fact]
    public void WritesTextThatCanBeExtractedAgain()
    {
        Document document = SimpleDocument(page => page.Content().Text("Extractable content"));

        using PdfDocument parsed = PdfDocument.Open(document.GeneratePdf());
        Page page = parsed.GetPage(1);

        Assert.Contains("Extractable", page.Text);
    }

    [Fact]
    public void ReportsThePageSizeItWasGiven()
    {
        Document document = SimpleDocument(page => page.Content().Text("A4"));

        using PdfDocument parsed = PdfDocument.Open(document.GeneratePdf());
        Page page = parsed.GetPage(1);

        // PdfPig reports points; A4 is 595.28 x 841.89.
        Assert.InRange(page.Width, 594, 597);
        Assert.InRange(page.Height, 840, 843);
    }

    [Fact]
    public void FlowsLongContentAcrossSeveralPages()
    {
        Document document = SimpleDocument(page => page.Content().Column(column =>
        {
            column.Spacing(5);

            for (int index = 0; index < 120; index++)
                column.Item().Text($"Line number {index} of the flowing content.");
        }));

        using PdfDocument parsed = PdfDocument.Open(document.GeneratePdf());

        Assert.True(parsed.NumberOfPages > 1, "120 lines should not fit on a single A4 page.");
    }

    [Fact]
    public void ResolvesTotalPageCountInTheFooter()
    {
        Document document = SimpleDocument(page =>
        {
            page.Footer().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });

            page.Content().Column(column =>
            {
                for (int index = 0; index < 120; index++)
                    column.Item().Text($"Content line {index}.");
            });
        });

        using PdfDocument parsed = PdfDocument.Open(document.GeneratePdf());
        int total = parsed.NumberOfPages;
        string firstPage = parsed.GetPage(1).Text;

        Assert.True(total > 1);
        Assert.Contains($"Page1of{total}", firstPage.Replace(" ", string.Empty));
    }

    [Fact]
    public void WritesDocumentMetadata()
    {
        Document document = SimpleDocument(page => page.Content().Text("Metadata"));
        document.Metadata.Title = "Integration Title";
        document.Metadata.Author = "Integration Author";

        using PdfDocument parsed = PdfDocument.Open(document.GeneratePdf());

        Assert.Equal("Integration Title", parsed.Information.Title);
        Assert.Equal("Integration Author", parsed.Information.Author);
    }

    [Fact]
    public void RendersATableAcrossPagesWithRepeatingHeaders()
    {
        Document document = SimpleDocument(page => page.Content().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(80);
                columns.RelativeColumn();
            });

            table.Header(header =>
            {
                header.Cell().Text("Code");
                header.Cell().Text("Description");
            });

            for (int index = 0; index < 90; index++)
            {
                table.Cell().Text($"C{index:D3}");
                table.Cell().Text($"Description for row {index}");
            }
        }));

        using PdfDocument parsed = PdfDocument.Open(document.GeneratePdf());

        Assert.True(parsed.NumberOfPages > 1);

        // The header band must be repeated, not just drawn once.
        foreach (Page page in parsed.GetPages())
            Assert.Contains("Description", page.Text);
    }

    [Fact]
    public void RendersMultiplePageRunsWithDifferentSizes()
    {
        Document document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size = PaperSizes.A4;
                page.Content().Text("Portrait");
            });

            container.Page(page =>
            {
                page.Size = PaperSizes.A4.Landscape();
                page.Content().Text("Landscape");
            });
        });

        using PdfDocument parsed = PdfDocument.Open(document.GeneratePdf());

        Assert.Equal(2, parsed.NumberOfPages);
        Assert.True(parsed.GetPage(2).Width > parsed.GetPage(2).Height);
    }

    [Fact]
    public void ContinuousPagesAdoptTheHeightOfTheirContent()
    {
        Document document = Document.Create(container => container.Page(page =>
        {
            page.Size = new Extent(300, 2000);
            page.IsContinuous = true;
            page.Margin = Sides.All(10);
            page.Content().Text("Receipt");
        }));

        using PdfDocument parsed = PdfDocument.Open(document.GeneratePdf());

        Assert.Single(parsed.GetPages());
        Assert.True(parsed.GetPage(1).Height < 100, "A one-line receipt should produce a short page.");
    }

    [Fact]
    public void AppliesStylingWithoutFailing()
    {
        Document document = SimpleDocument(page => page.Content().Column(column =>
        {
            column.Item().Text(text => text.Span("Bold").Bold().FontSize(24));
            column.Item().Text(text => text.Span("Coloured").FontColor(TestInks.Red));
            column.Item().Text(text => text.Span("Underlined").Underline());
            column.Item().Text(text => text.Span("Highlighted").BackgroundColor(TestInks.Yellow));
            column.Item().Background(TestInks.GreyLighten3).Padding(10).Text("On a background");
            column.Item().Border(1).BorderColor(TestInks.Black).Padding(5).Text("In a box");
        }));

        using PdfDocument parsed = PdfDocument.Open(document.GeneratePdf());
        string text = parsed.GetPage(1).Text;

        Assert.Contains("Bold", text);
        Assert.Contains("In a box", text);
    }

    [Fact]
    public void RendersListsWithTheirMarkers()
    {
        Document document = SimpleDocument(page => page.Content().Column(column =>
        {
            column.Spacing(10);

            column.Item().List(list =>
            {
                list.Item().Text("Bulleted one");
                list.Item().Text("Bulleted two");
            });

            column.Item().List(list =>
            {
                list.Ordered();
                list.Item().Text("Numbered one");
                list.Item().Text("Numbered two");
            });

            column.Item().List(list =>
            {
                list.Ordered(ListMarker.UpperRoman);
                list.Item().Text("Roman one");
            });
        }));

        using PdfDocument parsed = PdfDocument.Open(document.GeneratePdf());
        string text = parsed.GetPage(1).Text;

        Assert.Contains("Bulleted one", text);
        Assert.Contains("1.", text);
        Assert.Contains("2.", text);
        Assert.Contains("I.", text);
    }

    [Fact]
    public void RendersRoundedContainersAndScaledContent()
    {
        Document document = SimpleDocument(page => page.Content().Column(column =>
        {
            column.Spacing(8);

            column.Item().Background(TestInks.AmberLighten3).CornerRadius(8).Padding(10).Text("Rounded panel");
            column.Item().Border(2).BorderColor(TestInks.Indigo).CornerRadius(6).Padding(10).Text("Rounded outline");
            column.Item().Width(120).ScaleToFit().Text("This line is scaled down until it fits its box.");
            column.Item().FlipHorizontal().Text("Mirrored");
        }));

        using PdfDocument parsed = PdfDocument.Open(document.GeneratePdf());

        Assert.Contains("Rounded panel", parsed.GetPage(1).Text);
    }

    [Fact]
    public void HonoursParagraphIndentAndSpacing()
    {
        Document document = SimpleDocument(page => page.Content().Text(text =>
        {
            text.FirstLineIndent(24);
            text.ParagraphSpacing(10);
            text.Line("First paragraph opening line.");
            text.Span("Second paragraph opening line.");
        }));

        using PdfDocument parsed = PdfDocument.Open(document.GeneratePdf());
        List<Word> words = parsed.GetPage(1).GetWords().ToList();

        // Both paragraphs open at the indent, so neither starts at the left margin.
        List<Word> firstWords = words.Where(word => word.Text == "First" || word.Text == "Second").ToList();

        Assert.Equal(2, firstWords.Count);
        Assert.All(firstWords, word => Assert.True(word.BoundingBox.Left > 45, $"'{word.Text}' should be indented past the 30pt margin."));
    }

    [Fact]
    public void EnsureSpaceMovesContentRatherThanStrandingIt()
    {
        Document document = Document.Create(container => container.Page(page =>
        {
            page.Size = new Extent(300, 160);
            page.Margin = Sides.All(10);

            page.Content().Column(column =>
            {
                column.Item().Height(100).Text("Filler");
                column.Item().EnsureSpace(80).Text("Heading that must not be stranded");
            });
        }));

        using PdfDocument parsed = PdfDocument.Open(document.GeneratePdf());

        Assert.Equal(2, parsed.NumberOfPages);
        Assert.DoesNotContain("stranded", parsed.GetPage(1).Text);
        Assert.Contains("stranded", parsed.GetPage(2).Text);
    }

    [Fact]
    public void EmbedsExternalLinks()
    {
        Document document = SimpleDocument(page =>
            page.Content().Hyperlink("https://example.com").Text("Visit the site"));

        using PdfDocument parsed = PdfDocument.Open(document.GeneratePdf());
        List<Annotation> annotations = parsed.GetPage(1).GetAnnotations().ToList();

        Assert.NotEmpty(annotations);
    }
}
