namespace Rustaveli.Pdf.ConformanceTests.Guide;

/// <summary>The examples of docs/guide/text.md, as written there.</summary>
[Collection(WorkingDirectoryCollection.Name)]
public class TextTests
{
    [Fact]
    public void ParagraphsAndRuns()
    {
        Document document = Page(section =>
        {
            section.Body().Text(text =>
            {
                text.Run("Type is ");
                text.Run("set").Italic();
                text.Run(" in runs, ");
                text.Run("each styled").Bold().Ink(Ink.Hex("#C62828"));
                text.Run(" on its own: ");
                text.Run("underlined").Underline().StrokeStyle(StrokeStyle.Wavy).StrokeInk(Ink.Hex("#1565C0"));
                text.Run(", highlighted").Highlight(Ink.Hex("#FFF59D"));
                text.Run(", tracked out").Tracking(1.5f);
                text.Run(", or raised");
                text.Run("2").Superscript();
                text.Run(".");
            });
        });

        GuideOutput.Show(document, "paragraphs-and-runs", trim: true);

        // Tracked and raised letters are not gathered into words, so the runs are read letter by letter.
        using UglyToad.PdfPig.PdfDocument pdf = UglyToad.PdfPig.PdfDocument.Open(document.ExportPdf());
        string letters = string.Concat(pdf.GetPage(1).Letters.Select(letter => letter.Value)).Replace(" ", string.Empty, StringComparison.Ordinal);
        Assert.Equal("Typeissetinruns,eachstyledonitsown:underlined,highlighted,trackedout,orraised2.", letters);
    }

    [Fact]
    public void InheritedType()
    {
        Document document = Page(section =>
        {
            section.DefaultType = TypeStyle.Default.WithTypeface("Noto Sans").WithPointSize(10.5f).WithLeading(1.3f);

            section.Body().DefaultType(type => type.WithInk(Ink.Hex("#37474F"))).Text("Slate grey, in Noto Sans at 10.5 points.");
        });

        GuideOutput.Show(document, "inherited-type", trim: true);
        using UglyToad.PdfPig.PdfDocument pdf = UglyToad.PdfPig.PdfDocument.Open(document.ExportPdf());
        UglyToad.PdfPig.Content.Letter first = pdf.GetPage(1).Letters[0];
        Assert.Equal(10.5, first.PointSize, 1);
        Assert.Contains("NotoSans", first.FontName, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingAParagraph()
    {
        Document document = Page(section =>
        {
            SampleData sample = new SampleData(seed: 4);

            section.Body().Text(text =>
            {
                text.Justified();
                text.FirstLineIndent(18);
                text.SpaceBetweenParagraphs(6);
                text.Run(sample.Paragraphs(3));
            });
        });

        GuideOutput.Show(document, "setting-a-paragraph");
        using UglyToad.PdfPig.PdfDocument pdf = UglyToad.PdfPig.PdfDocument.Open(document.ExportPdf());
        IReadOnlyList<UglyToad.PdfPig.Content.Letter> letters = pdf.GetPage(1).Letters;

        // The first line is indented from the margin by the indent.
        Assert.Equal(36 + 18, letters[0].StartBaseLine.X, 0);
    }

    [Fact]
    public void PageNumbersReferencesAndLinks()
    {
        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = PaperSizes.A5;
            section.Margins = Sides.All(36);

            section.RunningFoot().Centered().Text(text =>
            {
                text.Folio(Numerals.LowerRoman);
                text.Run(" of ");
                text.PageCount(Numerals.LowerRoman);
            });

            section.Body().Stack(stack =>
            {
                stack.Add().Text(text =>
                {
                    text.Run("The method is set out on page ");
                    text.FolioOf("method");
                    text.Run(". ");
                    text.CrossReference("Read it now.", "method").Underline();
                    text.Run(" The data is at ");
                    text.Link("example.org", "https://example.org").Underline();
                    text.Run(".");
                });

                stack.Add().NewPage();
                stack.Add().Anchor("method").Bookmark("Method").Text(text => text.Run("Method").PointSize(16));
                stack.Add().Text(new SampleData(seed: 2).Paragraphs(2));
            });
        }));

        GuideOutput.Show(document, "page-numbers-and-references");
        byte[] bytes = document.ExportPdf();
        IReadOnlyList<string> pages = GuideReader.PageTexts(bytes);

        Assert.StartsWith("The method is set out on page 2. Read it now. The data is at example.org.", pages[0], StringComparison.Ordinal);
        Assert.EndsWith("i of ii", pages[0], StringComparison.Ordinal);
        Assert.EndsWith("ii of ii", pages[1], StringComparison.Ordinal);

        using UglyToad.PdfPig.PdfDocument pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
        Assert.True(pdf.TryGetBookmarks(out UglyToad.PdfPig.Outline.Bookmarks? bookmarks));
        Assert.Equal("Method", Assert.Single(bookmarks.Roots).Title);
        Assert.Contains(pdf.GetPage(1).GetHyperlinks(), link => link.Uri == "https://example.org");
    }

    [Fact]
    public void Typefaces()
    {
        using TemporaryWorkingDirectory directory = new TemporaryWorkingDirectory();
        CopyFonts("NotoSans-Regular.ttf", "NotoSansGeorgian-Regular.ttf");

        TypefaceLibrary typefaces = new TypefaceLibrary();
        typefaces.RegisterFile("fonts/NotoSans-Regular.ttf");
        typefaces.RegisterFile("fonts/NotoSansGeorgian-Regular.ttf", "Georgian");
        typefaces.Fallbacks = ["Georgian"];

        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.DefaultType = TypeStyle.Default.WithTypeface("Noto Sans");
            section.Body().Text("Hello — გამარჯობა");
        }));

        byte[] pdf = document.ExportPdf(new PdfExportOptions { Typefaces = typefaces, RequireEveryGlyph = true });

        GuideOutput.Show(document, "typefaces", trim: true, typefaces: typefaces);

        Assert.Equal("Hello — გამარჯობა", GuideReader.Text(pdf));
    }

    [Fact]
    public void ComplexScripts()
    {
        using TemporaryWorkingDirectory directory = new TemporaryWorkingDirectory();
        CopyFonts("NotoSansArabic-Regular.ttf");

        TypefaceLibrary typefaces = new TypefaceLibrary().ShapeComplexScripts();
        typefaces.RegisterFile("fonts/NotoSansArabic-Regular.ttf", "Arabic");

        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.ReadingDirection = ReadingDirection.RightToLeft;
            section.DefaultType = TypeStyle.Default.WithTypeface("Arabic").WithPointSize(14);
            section.Body().Text("مرحبا بالعالم");
        }));

        byte[] pdf = document.ExportPdf(new PdfExportOptions { Typefaces = typefaces });

        GuideOutput.Show(document, "complex-scripts", trim: true, typefaces: typefaces);

        // Joined letters take their contextual forms, so the page is read letter by letter against what was set.
        using UglyToad.PdfPig.PdfDocument read = UglyToad.PdfPig.PdfDocument.Open(pdf);
        string letters = string.Concat(read.GetPage(1).Letters.Select(letter => letter.Value)).Replace(" ", string.Empty, StringComparison.Ordinal);
        Assert.Equal("مرحبا بالعالم".Replace(" ", string.Empty, StringComparison.Ordinal).Length, letters.Length);
    }

    [Fact]
    public void OpenTypeFeatures()
    {
        Document document = Page(section =>
        {
            section.Body().Text(text =>
            {
                text.Line("Figures: 0123456789").OldstyleFigures();
                text.Line("In a column: 1111.11").TabularFigures();
                text.Line("Small capitals").SmallCapitals();
                text.Line("No ligatures in office").Ligatures(false);
                text.Line("Stylistic set one").Feature("ss01");
            });
        });

        GuideOutput.Show(document, "opentype-features", trim: true);
        string text = GuideReader.Text(document.ExportPdf());

        Assert.Contains("No ligatures in office", text, StringComparison.Ordinal);
        Assert.Contains("In a column: 1111.11", text, StringComparison.Ordinal);
    }

    [Fact]
    public void StyleSheets()
    {
        Document document = Document.Compose(composition =>
        {
            composition.Styles
                .DefineType("Heading", type => type.WithPointSize(18).Bold())
                .DefineType("Subheading", type => type.WithPointSize(13), basedOn: "Heading")
                .DefineParagraph("Lead", text => text.Justified())
                .DefineFrame("Callout", frame => frame.Fill(Ink.Hex("#F1F8E9")).Stroke(0.5f).Inset(10));

            composition.Section(section =>
            {
                section.Trim = PaperSizes.A5;
                section.Margins = Sides.All(36);

                section.Body().Stack(stack =>
                {
                    stack.SpaceBetween(8);
                    stack.Add().Text(text => text.Run("Findings").Style("Heading"));
                    stack.Add().Text(text => text.Run("In brief").Style("Subheading"));
                    stack.Add().Text(text =>
                    {
                        text.Style("Lead");
                        text.Run(new SampleData(seed: 9).Paragraph());
                    });
                    stack.Add().Style("Callout").Text("Styles are defined once and applied by name.");
                });
            });
        });

        GuideOutput.Show(document, "style-sheets", trim: true);
        using UglyToad.PdfPig.PdfDocument pdf = UglyToad.PdfPig.PdfDocument.Open(document.ExportPdf());
        List<UglyToad.PdfPig.Content.Word> words = pdf.GetPage(1).GetWords().ToList();
        UglyToad.PdfPig.Content.Letter findings = words.First(word => word.Text == "Findings").Letters[0];
        UglyToad.PdfPig.Content.Letter brief = words.First(word => word.Text == "brief").Letters[0];

        Assert.Equal(18, findings.PointSize, 1);
        Assert.Equal(13, brief.PointSize, 1);
        Assert.Contains("Bold", brief.FontName, StringComparison.Ordinal);
        Assert.Contains(words, word => word.Text == "applied");
    }

    private static void CopyFonts(params string[] names)
    {
        Directory.CreateDirectory("fonts");

        foreach (string name in names)
            File.Copy(Path.Combine(AppContext.BaseDirectory, "assets", "fonts", name), Path.Combine("fonts", name));
    }

    /// <summary>An A5 page, as the examples that begin at the body are set on.</summary>
    private static Document Page(Action<Section> compose) => Document.Compose(composition => composition.Section(section =>
    {
        section.Trim = PaperSizes.A5;
        section.Margins = Sides.All(36);
        compose(section);
    }));
}
