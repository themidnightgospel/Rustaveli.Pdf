using System.Globalization;

namespace Rustaveli.Pdf.ConformanceTests.Guide;

/// <summary>The examples of docs/guide/layout.md, as written there.</summary>
public class LayoutTests
{
    [Fact]
    public void StacksAndColumns()
    {
        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = PaperSizes.A4;
            section.Margins = Sides.All(40);

            section.Body().Stack(stack =>
            {
                stack.SpaceBetween(12);

                stack.Add().Columns(columns =>
                {
                    columns.Gutter(20);
                    columns.Share(2).DefaultType(type => type.Bold()).Text("Rustaveli Printing Ltd");
                    columns.Natural().Text("Invoice 2026-041");
                });

                stack.Add().Rule(0.5f, Ink.Hex("#9E9E9E"));

                stack.Add().Columns(columns =>
                {
                    columns.Fixed(120).Text("Billed to");
                    columns.Share().Text("Nino Kapanadze, 3 Chavchavadze Avenue, Tbilisi");
                });
            });
        }));

        Assert.Equal(
            "Rustaveli Printing Ltd Invoice 2026-041 Billed to Nino Kapanadze, 3 Chavchavadze Avenue, Tbilisi",
            GuideReader.Text(document.ExportPdf()));
    }

    [Fact]
    public void Tables()
    {
        Document document = Page(section =>
        {
            section.Body().Table(table =>
            {
                table.Columns(columns =>
                {
                    columns.Fixed(30);
                    columns.Share(3);
                    columns.Share();
                });

                table.HeaderRows(header =>
                {
                    header.Cell().Text("#");
                    header.Cell().Text("Item");
                    header.Cell().FlushRight().Text("Price");
                });

                for (int line = 1; line <= 60; line++)
                {
                    table.Cell().Text(line.ToString(CultureInfo.InvariantCulture));
                    table.Cell().Text("Item " + line.ToString(CultureInfo.InvariantCulture));
                    table.Cell().FlushRight().Text((line * 2.5).ToString("0.00", CultureInfo.InvariantCulture));
                }

                table.FooterRows(footer =>
                {
                    footer.Cell().SpanColumns(2).Text("Continued overleaf");
                    footer.Cell().FlushRight().Text("—");
                });
            });
        });

        IReadOnlyList<string> pages = GuideReader.PageTexts(document.ExportPdf());

        Assert.True(pages.Count > 1);
        Assert.All(pages, page => Assert.StartsWith("# Item Price", page, StringComparison.Ordinal));
        Assert.All(pages, page => Assert.EndsWith("Continued overleaf —", page, StringComparison.Ordinal));
        Assert.Contains("60 Item 60 150.00", pages[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void CellsThatSpanAndArePlaced()
    {
        Document document = Page(section =>
        {
            section.Body().Table(table =>
            {
                table.Columns(columns =>
                {
                    columns.Share();
                    columns.Share();
                    columns.Share();
                });

                table.Cell().AtRow(1).AtColumn(1).SpanRows(2).Fill(Ink.Hex("#FFF3E0")).Inset(6).Text("Two rows tall");
                table.Cell().Stroke(0.5f).Inset(6).Text("B");
                table.Cell().Stroke(0.5f).Inset(6).Text("C");
                table.Cell().SpanColumns(2).Stroke(0.5f).Inset(6).Text("Two columns wide");
            });
        });

        Assert.Equal("Two rows tall B C Two columns wide", GuideReader.Text(document.ExportPdf()));
    }

    [Fact]
    public void Lists()
    {
        Document document = Page(section =>
        {
            section.Body().List(list =>
            {
                list.Numbered(ListNumbering.LowerRoman);
                list.SpaceBetween(4);
                list.Add().Text("Read the brief.");
                list.Add().Text("Set the type.");
                list.Add().Text("Send the proofs.");
            });
        });

        Assert.Equal("i. Read the brief. ii. Set the type. iii. Send the proofs.", GuideReader.Text(document.ExportPdf()));
    }

    [Fact]
    public void Layers()
    {
        Document document = Page(section =>
        {
            section.Body().Layered(layers =>
            {
                layers.Layer().Middle().Centered().Rotate(-30).Text(text =>
                {
                    text.Run("DRAFT").PointSize(72).Ink(Ink.Hex("#E0E0E0"));
                });
                layers.BaseLayer().Text(new SampleData(seed: 7).Paragraphs(3));
            });
        });

        // Turned letters are not gathered into words, so the watermark is read letter by letter.
        using UglyToad.PdfPig.PdfDocument pdf = UglyToad.PdfPig.PdfDocument.Open(document.ExportPdf());
        string letters = string.Concat(pdf.GetPage(1).Letters.Select(letter => letter.Value));
        Assert.StartsWith("DRAFT", letters, StringComparison.Ordinal);
        Assert.Equal(1, pdf.NumberOfPages);
    }

    [Fact]
    public void Bands()
    {
        Document document = Page(section =>
        {
            section.Body().Banded(bands =>
            {
                bands.Head().InsetBottom(6).DefaultType(type => type.Bold()).Text("Transactions, continued");
                bands.Body().Text(new SampleData(seed: 3).Paragraphs(12));
                bands.Foot().InsetTop(6).FlushRight().Text("Balance carried forward");
            });
        });

        IReadOnlyList<string> pages = GuideReader.PageTexts(document.ExportPdf());

        Assert.True(pages.Count > 1);
        Assert.All(pages, page => Assert.StartsWith("Transactions, continued", page, StringComparison.Ordinal));
        Assert.All(pages, page => Assert.EndsWith("Balance carried forward", page, StringComparison.Ordinal));
    }

    [Fact]
    public void GridsFlowsAndFlowingColumns()
    {
        Document document = Page(section =>
        {
            section.Body().Stack(stack =>
            {
                stack.SpaceBetween(16);

                stack.Add().Grid(grid =>
                {
                    grid.Columns(4);
                    grid.Gutter(8);
                    grid.SpaceBetweenRows(8);
                    grid.Cell(span: 2).Fill(Ink.Hex("#E8EAF6")).Inset(8).Text("Half");
                    grid.Cell().Fill(Ink.Hex("#E8EAF6")).Inset(8).Text("Quarter");
                    grid.Cell().Fill(Ink.Hex("#E8EAF6")).Inset(8).Text("Quarter");
                });

                stack.Add().Flow(flow =>
                {
                    flow.Gutter(6);
                    flow.SpaceBetweenLines(6);

                    foreach (string tag in new[] { "typesetting", "layout", "pdf", "invoices", "reports" })
                        flow.Add().Stroke(0.5f).RoundCorners(8).InsetHorizontal(8).InsetVertical(2).Text(tag);
                });

                stack.Add().FlowColumns(columns =>
                {
                    columns.Columns(2);
                    columns.Gutter(18);
                    columns.Balanced();
                    columns.Story().Text(new SampleData(seed: 11).Paragraphs(4));
                    columns.Between().VerticalRule(0.5f);
                });
            });
        });

        string text = GuideReader.Text(document.ExportPdf());

        Assert.StartsWith("Half Quarter Quarter typesetting layout pdf invoices reports", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FittingAFrameToItsContent()
    {
        Document document = Page(section =>
        {
            section.Body().FitToContent().Fill(Ink.Hex("#FFF9C4")).Inset(4).Text("Just this much yellow");
        });

        Assert.Equal("Just this much yellow", GuideReader.Text(document.ExportPdf()));
    }

    [Fact]
    public void FlowAcrossPages()
    {
        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = PaperSizes.A5;
            section.Margins = Sides.All(36);

            section.RunningHead().When(page => !page.IsFirst).FlushRight().Text("Annual report, continued");

            section.Body().Stack(stack =>
            {
                SampleData sample = new SampleData(seed: 5);

                stack.SpaceBetween(10);
                stack.Add().Text(text => text.Run("Annual report").PointSize(20));
                stack.Add().KeepTogether().Text(sample.Paragraphs(2));
                stack.Add().Text(sample.Paragraphs(6));
                stack.Add().NewPage();
                stack.Add().RequireSpace(150).Text(text => text.Run("Appendix").PointSize(16));
                stack.Add().Text(sample.Paragraphs(2));
            });
        }));

        IReadOnlyList<string> pages = GuideReader.PageTexts(document.ExportPdf());

        Assert.StartsWith("Annual report ", pages[0], StringComparison.Ordinal);
        Assert.DoesNotContain("continued", pages[0], StringComparison.Ordinal);
        Assert.All(pages.Skip(1), page => Assert.StartsWith("Annual report, continued", page, StringComparison.Ordinal));
        Assert.StartsWith("Annual report, continued Appendix", pages.Single(page => page.Contains("Appendix", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    /// <summary>An A5 page, as the examples that begin at the body are set on.</summary>
    private static Document Page(Action<Section> compose) => Document.Compose(composition => composition.Section(section =>
    {
        section.Trim = PaperSizes.A5;
        section.Margins = Sides.All(36);
        compose(section);
    }));
}
