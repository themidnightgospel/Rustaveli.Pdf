using Rustaveli.Pdf.ConformanceTests.Specimens;
using Rustaveli.Pdf.ConformanceTests.Validation;

namespace Rustaveli.Pdf.ConformanceTests;

/// <summary>
/// Every specimen written to the standards it can claim — PDF/A at each level, PDF/UA — meets them as veraPDF, the
/// reference validator, reads them.
/// </summary>
public class StandardsTests
{
    private static readonly (string Name, PdfExportOptions Options)[] Claims =
    [
        ("a2b", new PdfExportOptions { Conformance = PdfAConformance.PdfA2B }),
        ("a3u", new PdfExportOptions { Conformance = PdfAConformance.PdfA3U }),
        ("a2a-ua1", new PdfExportOptions { Conformance = PdfAConformance.PdfA2A, Accessibility = PdfUAConformance.PdfUA1 }),
        ("ua1", new PdfExportOptions { Accessibility = PdfUAConformance.PdfUA1 }),
    ];

    [Fact]
    public void EverySpecimenMeetsTheStandardsItClaims()
    {
        TestFonts.EnsureRegistered();
        Dictionary<string, byte[]> files = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (Specimen specimen in SpecimenCatalog.All.Append(new Specimen("structure", Structured)))
        {
            foreach ((string claim, PdfExportOptions options) in Claims)
            {
                Document document = specimen.Build();
                document.Info.Title ??= specimen.Name;
                document.Info.Language ??= "en";
                options.ImageProcessor = SkiaImageProcessor.Instance;
                files.Add($"{specimen.Name}-{claim}", document.ExportPdf(options));
            }
        }

        IReadOnlyDictionary<string, IReadOnlyList<string>> failures = VeraPdf.Validate(files);

        Assert.Equal(files.Keys.OrderBy(name => name, StringComparer.Ordinal), failures.Keys.OrderBy(name => name, StringComparer.Ordinal));

        string[] broken = failures.Where(file => file.Value.Count > 0)
            .SelectMany(file => file.Value.Select(rule => $"{file.Key}: {rule}"))
            .ToArray();

        Assert.True(broken.Length == 0, "veraPDF found:\n" + string.Join("\n", broken));
    }

    [Fact]
    public void MetadataWithLineBreaksSaysWhatTheInformationDictionarySays()
    {
        // An XML parser reads a raw carriage return as a line feed, and PDF/A compares the two entry by entry.
        TestFonts.EnsureRegistered();
        Document document = SpecimenCatalog.All[0].Build();
        document.Info.Title = "Q3\r\nReport";
        document.Info.Subject = "Carriage\rreturn";
        byte[] pdf = document.ExportPdf(new PdfExportOptions { Conformance = PdfAConformance.PdfA2B, ImageProcessor = SkiaImageProcessor.Instance });

        IReadOnlyList<string> broken = VeraPdf.Validate(new Dictionary<string, byte[]> { ["line-breaks"] = pdf })["line-breaks"];

        Assert.True(broken.Count == 0, "veraPDF found:\n" + string.Join("\n", broken));
    }

    [Fact]
    public void MetadataXmlCannotHoldIsRefused()
    {
        TestFonts.EnsureRegistered();
        Document document = SpecimenCatalog.All[0].Build();
        document.Info.Title = "A\u0001B";

        Assert.Throws<InvalidOperationException>(() => document.ExportPdf(new PdfExportOptions { Conformance = PdfAConformance.PdfA2B }));
    }

    /// <summary>
    /// Everything tagging touches: headings, a paragraph with a link, a figure, decoration, a list, a table spanning pages
    /// with repeated headings and row headings, a change of language and untagged content, under a running head and foot.
    /// </summary>
    private static Document Structured()
    {
        RasterImage picture = RasterImage.FromFile(Path.Combine(AppContext.BaseDirectory, "assets", "images", "basn2c08.png"));

        return Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(400, 300);
            section.Margins = new Sides(30, 30, 30, 30);
            section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(10);
            section.RunningHead().Text("Quarterly report");
            section.RunningFoot().Text(text =>
            {
                text.Run("Page ");
                text.Folio();
            });

            section.Body().Stack(stack =>
            {
                stack.Add().Tagged(ContentTag.Heading(1)).Text("Accessible documents");
                stack.Add().Text(text =>
                {
                    text.Run("Tagged PDF records structure; see ");
                    text.Link("the standard", "https://www.iso.org/standard/64599.html");
                    text.Run(".");
                });
                stack.Add().Height(4).Fill(Ink.Rgb(200, 200, 200)).Blank();
                stack.Add().Tagged(ContentTag.Figure("A test pattern")).Width(40).Image(picture);
                stack.Add().List(list =>
                {
                    list.Add().Text("First point");
                    list.Add().Text("Second point");
                });
                stack.Add().Tagged(ContentTag.Table).Table(table =>
                {
                    table.Columns(columns =>
                    {
                        columns.Share();
                        columns.Share();
                    });
                    table.HeaderRows(header =>
                    {
                        header.Cell().Text("Name");
                        header.Cell().Text("Value");
                    });

                    for (int row = 1; row <= 25; row++)
                    {
                        table.Cell().RowHeading().Text("Row " + row);
                        table.Cell().Text((row * 7).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                });
                stack.Add().Tagged(ContentTag.Heading(2)).Text("Elsewhere");
                stack.Add().Language("ka").Text("გამარჯობა");
                stack.Add().Untagged().Text("Decoration");
            });
        }));
    }
}
