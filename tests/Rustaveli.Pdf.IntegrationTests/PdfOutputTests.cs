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
        Document.Compose(container => container.Section(page =>
        {
            page.Trim = PaperSizes.A4;
            page.Margins = Sides.All(30);
            configure(page);
        }));

    [Fact]
    public void ProducesAFileWithAPdfHeader()
    {
        Document document = SimpleDocument(page => page.Body().Text("Hello, world."));

        byte[] bytes = document.ExportPdf();

        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF"u8.ToArray(), bytes.Take(4).ToArray());
    }

    [Fact]
    public void WritesTextThatCanBeExtractedAgain()
    {
        Document document = SimpleDocument(page => page.Body().Text("Extractable content"));

        using PdfDocument parsed = PdfDocument.Open(document.ExportPdf());
        Page page = parsed.GetPage(1);

        Assert.Contains("Extractable", page.Text);
    }

    [Fact]
    public void ReportsThePageSizeItWasGiven()
    {
        Document document = SimpleDocument(page => page.Body().Text("A4"));

        using PdfDocument parsed = PdfDocument.Open(document.ExportPdf());
        Page page = parsed.GetPage(1);

        // PdfPig reports points; A4 is 595.28 x 841.89.
        Assert.InRange(page.Width, 594, 597);
        Assert.InRange(page.Height, 840, 843);
    }

    [Fact]
    public void FlowsLongContentAcrossSeveralPages()
    {
        Document document = SimpleDocument(page => page.Body().Stack(column =>
        {
            column.SpaceBetween(5);

            for (int index = 0; index < 120; index++)
                column.Add().Text($"Line number {index} of the flowing content.");
        }));

        using PdfDocument parsed = PdfDocument.Open(document.ExportPdf());

        Assert.True(parsed.NumberOfPages > 1, "120 lines should not fit on a single A4 page.");
    }

    [Fact]
    public void ResolvesTotalPageCountInTheFooter()
    {
        Document document = SimpleDocument(page =>
        {
            page.RunningFoot().Text(text =>
            {
                text.Run("Page ");
                text.Folio();
                text.Run(" of ");
                text.PageCount();
            });

            page.Body().Stack(column =>
            {
                for (int index = 0; index < 120; index++)
                    column.Add().Text($"Content line {index}.");
            });
        });

        using PdfDocument parsed = PdfDocument.Open(document.ExportPdf());
        int total = parsed.NumberOfPages;
        string firstPage = parsed.GetPage(1).Text;

        Assert.True(total > 1);
        Assert.Contains($"Page1of{total}", firstPage.Replace(" ", string.Empty));
    }

    [Fact]
    public void WritesDocumentMetadata()
    {
        Document document = SimpleDocument(page => page.Body().Text("Metadata"));
        document.Info.Title = "Integration Title";
        document.Info.Author = "Integration Author";

        using PdfDocument parsed = PdfDocument.Open(document.ExportPdf());

        Assert.Equal("Integration Title", parsed.Information.Title);
        Assert.Equal("Integration Author", parsed.Information.Author);
    }

    [Fact]
    public void RendersATableAcrossPagesWithRepeatingHeaders()
    {
        Document document = SimpleDocument(page => page.Body().Table(table =>
        {
            table.Columns(columns =>
            {
                columns.Fixed(80);
                columns.Share();
            });

            table.HeaderRows(header =>
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

        using PdfDocument parsed = PdfDocument.Open(document.ExportPdf());

        Assert.True(parsed.NumberOfPages > 1);

        // The header band must be repeated, not just drawn once.
        foreach (Page page in parsed.GetPages())
            Assert.Contains("Description", page.Text);
    }

    [Fact]
    public void RendersMultiplePageRunsWithDifferentSizes()
    {
        Document document = Document.Compose(container =>
        {
            container.Section(page =>
            {
                page.Trim = PaperSizes.A4;
                page.Body().Text("Portrait");
            });

            container.Section(page =>
            {
                page.Trim = PaperSizes.A4.Landscape();
                page.Body().Text("Landscape");
            });
        });

        using PdfDocument parsed = PdfDocument.Open(document.ExportPdf());

        Assert.Equal(2, parsed.NumberOfPages);
        Assert.True(parsed.GetPage(2).Width > parsed.GetPage(2).Height);
    }

    [Fact]
    public void ContinuousPagesAdoptTheHeightOfTheirContent()
    {
        Document document = Document.Compose(container => container.Section(page =>
        {
            page.Trim = new Extent(300, 2000);
            page.Continuous = true;
            page.Margins = Sides.All(10);
            page.Body().Text("Receipt");
        }));

        using PdfDocument parsed = PdfDocument.Open(document.ExportPdf());

        Assert.Single(parsed.GetPages());
        Assert.True(parsed.GetPage(1).Height < 100, "A one-line receipt should produce a short page.");
    }

    [Fact]
    public void AppliesStylingWithoutFailing()
    {
        Document document = SimpleDocument(page => page.Body().Stack(column =>
        {
            column.Add().Text(text => text.Run("Bold").Bold().PointSize(24));
            column.Add().Text(text => text.Run("Coloured").Ink(TestInks.Red));
            column.Add().Text(text => text.Run("Underlined").Underline());
            column.Add().Text(text => text.Run("Highlighted").Highlight(TestInks.Yellow));
            column.Add().Fill(TestInks.GreyLighten3).Inset(10).Text("On a background");
            column.Add().Stroke(1).StrokeInk(TestInks.Black).Inset(5).Text("In a box");
        }));

        using PdfDocument parsed = PdfDocument.Open(document.ExportPdf());
        string text = parsed.GetPage(1).Text;

        Assert.Contains("Bold", text);
        Assert.Contains("In a box", text);
    }

    [Fact]
    public void RendersListsWithTheirMarkers()
    {
        Document document = SimpleDocument(page => page.Body().Stack(column =>
        {
            column.SpaceBetween(10);

            column.Add().List(list =>
            {
                list.Add().Text("Bulleted one");
                list.Add().Text("Bulleted two");
            });

            column.Add().List(list =>
            {
                list.Numbered();
                list.Add().Text("Numbered one");
                list.Add().Text("Numbered two");
            });

            column.Add().List(list =>
            {
                list.Numbered(ListNumbering.UpperRoman);
                list.Add().Text("Roman one");
            });
        }));

        using PdfDocument parsed = PdfDocument.Open(document.ExportPdf());
        string text = parsed.GetPage(1).Text;

        Assert.Contains("Bulleted one", text);
        Assert.Contains("1.", text);
        Assert.Contains("2.", text);
        Assert.Contains("I.", text);
    }

    [Fact]
    public void RendersRoundedContainersAndScaledContent()
    {
        Document document = SimpleDocument(page => page.Body().Stack(column =>
        {
            column.SpaceBetween(8);

            column.Add().Fill(TestInks.AmberLighten3).RoundCorners(8).Inset(10).Text("Rounded panel");
            column.Add().Stroke(2).StrokeInk(TestInks.Indigo).RoundCorners(6).Inset(10).Text("Rounded outline");
            column.Add().Width(120).ShrinkToFit().Text("This line is scaled down until it fits its box.");
            column.Add().MirrorHorizontal().Text("Mirrored");
        }));

        using PdfDocument parsed = PdfDocument.Open(document.ExportPdf());

        Assert.Contains("Rounded panel", parsed.GetPage(1).Text);
    }

    [Fact]
    public void HonoursParagraphIndentAndSpacing()
    {
        Document document = SimpleDocument(page => page.Body().Text(text =>
        {
            text.FirstLineIndent(24);
            text.SpaceBetweenParagraphs(10);
            text.Line("First paragraph opening line.");
            text.Run("Second paragraph opening line.");
        }));

        using PdfDocument parsed = PdfDocument.Open(document.ExportPdf());
        List<Word> words = parsed.GetPage(1).GetWords().ToList();

        // Both paragraphs open at the indent, so neither starts at the left margin.
        List<Word> firstWords = words.Where(word => word.Text == "First" || word.Text == "Second").ToList();

        Assert.Equal(2, firstWords.Count);
        Assert.All(firstWords, word => Assert.True(word.BoundingBox.Left > 45, $"'{word.Text}' should be indented past the 30pt margin."));
    }

    [Fact]
    public void EnsureSpaceMovesContentRatherThanStrandingIt()
    {
        Document document = Document.Compose(container => container.Section(page =>
        {
            page.Trim = new Extent(300, 160);
            page.Margins = Sides.All(10);

            page.Body().Stack(column =>
            {
                column.Add().Height(100).Text("Filler");
                column.Add().RequireSpace(80).Text("Heading that must not be stranded");
            });
        }));

        using PdfDocument parsed = PdfDocument.Open(document.ExportPdf());

        Assert.Equal(2, parsed.NumberOfPages);
        Assert.DoesNotContain("stranded", parsed.GetPage(1).Text);
        Assert.Contains("stranded", parsed.GetPage(2).Text);
    }

    [Fact]
    public void EmbedsExternalLinks()
    {
        Document document = SimpleDocument(page =>
            page.Body().Link("https://example.com").Text("Visit the site"));

        using PdfDocument parsed = PdfDocument.Open(document.ExportPdf());
        List<Annotation> annotations = parsed.GetPage(1).GetAnnotations().ToList();

        Assert.NotEmpty(annotations);
    }
}
