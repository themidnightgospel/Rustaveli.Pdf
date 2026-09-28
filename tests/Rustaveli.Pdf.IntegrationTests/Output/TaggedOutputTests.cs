using System.Text;
using System.Text.RegularExpressions;

namespace Rustaveli.Pdf.IntegrationTests.Output;

/// <summary>
/// Tagged PDF as written: content marked for the element it belongs to or as decoration, the structure tree and parent
/// tree over it, links placed in the structure, and what PDF/UA and the accessible levels of PDF/A declare.
/// </summary>
public class TaggedOutputTests
{
    private static string Export(Action<Section> compose, PdfExportOptions? options = null, Action<DocumentInfo>? info = null)
    {
        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(300, 200);
            section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans);
            compose(section);
        }));

        document.Info.Title = "Tagged";
        document.Info.Language = "en";
        info?.Invoke(document.Info);

        options ??= new PdfExportOptions { Tagged = true };
        options.Compress = false;
        return Encoding.Latin1.GetString(document.ExportPdf(options));
    }

    private static string Body(Action<StackComposer> compose, PdfExportOptions? options = null) =>
        Export(section => section.Body().Stack(compose), options);

    /// <summary>The page content streams: those that draw.</summary>
    private static List<string> Contents(string pdf) =>
        Regex.Matches(pdf, @"stream\r?\n([\s\S]*?)endstream")
            .Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .Where(stream => Regex.IsMatch(stream, @"\b(BT|BDC|BMC|re|Do)\n"))
            .ToList();

    /// <summary>The operators of every content stream, in order, one per line as the writer puts them.</summary>
    private static List<string> Operators(string pdf) =>
        Contents(pdf).SelectMany(stream => stream.Split('\n'))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Select(line => line.Split(' ', '>').Last())
            .ToList();

    /// <summary>The tag of the marked sequence open at <paramref name="index"/> in a content stream, or null for none.</summary>
    private static string? MarkedAt(string content, int index)
    {
        Stack<string> open = new Stack<string>();

        foreach (Match line in Regex.Matches(content.Substring(0, index), @"^(?:/(\w+)[^\n]*\b(BMC|BDC)|EMC)$", RegexOptions.Multiline).Cast<Match>())
        {
            if (line.Value == "EMC")
                open.Pop();
            else
                open.Push(line.Groups[1].Value);
        }

        return open.Count == 0 ? null : open.Peek();
    }

    [Fact]
    public void AnUntaggedExportRecordsNoStructure()
    {
        string pdf = Body(stack => stack.Add().Tagged(ContentTag.Heading(1)).Text("Title"), new PdfExportOptions());

        Assert.DoesNotContain("StructTreeRoot", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("MarkInfo", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("StructParents", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("/Tabs", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain(Operators(pdf), line => line is "BDC" or "BMC" or "EMC");
    }

    [Fact]
    public void ATaggedExportIsMarkedTaggedWithItsTreeAndPageKeys()
    {
        string pdf = Body(stack => stack.Add().Text("Hello"));

        Assert.Matches(@"/StructTreeRoot \d+ 0 R", pdf);
        Assert.Matches(@"/MarkInfo\s*<<\s*/Marked\s+true\s*>>", pdf);
        Assert.Matches(@"/Type\s*/StructTreeRoot", pdf);
        Assert.Matches(@"/ParentTree\s*<<\s*/Nums\s*\[\s*0\s*\[\s*\d+ 0 R\s*\]\s*\]", pdf);
        Assert.Matches(@"/ParentTreeNextKey 1\b", pdf);
        Assert.Matches(@"/StructParents 0\b", pdf);
        Assert.Matches(@"/Tabs\s*/S\b", pdf);
        Assert.Matches(@"/S\s*/Document", pdf);
        Assert.Matches(@"/S\s*/P\b", pdf);
    }

    [Fact]
    public void TextIsMarkedForItsElementAndOneSequenceServesConsecutiveRuns()
    {
        string pdf = Body(stack => stack.Add().Text(text =>
        {
            text.Run("Plain ");
            text.Run("bold").Bold();
        }));

        string content = Assert.Single(Contents(pdf));
        Assert.Single(Regex.Matches(content, "BDC"));
        Assert.Matches(@"^/P<</MCID 0>>BDC\n[\s\S]*BT[\s\S]*ET[\s\S]*ET\nEMC\n", content.Substring(content.IndexOf("/P<<", StringComparison.Ordinal)));
    }

    [Fact]
    public void DrawingsAndImagesOutsideAFigureAreDecorationAndInsideItBelongToIt()
    {
        RasterImage image = RasterImage.FromFile(Path.Combine(AppContext.BaseDirectory, "assets", "images", "basn2c08.png"));

        string pdf = Body(stack =>
        {
            stack.Add().Height(10).Fill(Ink.Rgb(200, 0, 0)).Blank();
            stack.Add().Width(20).Image(image);
            stack.Add().Tagged(ContentTag.Figure("A pattern")).Width(20).Image(image);
        });

        string content = Assert.Single(Contents(pdf));
        int[] images = Regex.Matches(content, @" Do\n").Cast<Match>().Select(match => match.Index).ToArray();

        Assert.Equal("Artifact", MarkedAt(content, content.IndexOf(" rg\n", StringComparison.Ordinal)));
        Assert.Equal(2, images.Length);
        Assert.Equal("Artifact", MarkedAt(content, images[0]));
        Assert.Equal("Figure", MarkedAt(content, images[1]));
        Assert.Matches(@"/Figure<</MCID 0>>BDC\nq\n[^\n]* cm\n/\w+ Do\nQ\n", content);
        Assert.Matches(@"/Alt\s*\(A pattern\)", pdf);
    }

    [Fact]
    public void MarkedSequencesNestWithinSavedStates()
    {
        string pdf = Body(stack =>
        {
            stack.Add().Text("Before");
            stack.Add().Rotate(10).Text("Turned");
            stack.Add().Height(20).Stroke(1).Text("Framed");
            stack.Add().Tagged(ContentTag.Figure("Lines")).Stack(lines =>
            {
                lines.Add().Height(4).Rule(ink: Ink.Rgb(0, 0, 0), style: StrokeStyle.Dotted);
                lines.Add().Height(4).Rule(1, Ink.Rgb(0, 0, 0), [2f, 1f]);
            });
            stack.Add().Text("After");
        });

        Stack<string> open = new Stack<string>();

        foreach (string line in Operators(pdf))
        {
            switch (line)
            {
                case "q":
                case "BDC":
                case "BMC":
                    open.Push(line == "q" ? "q" : "mark");
                    break;

                case "Q":
                    Assert.Equal("q", open.Pop());
                    break;

                case "EMC":
                    Assert.Equal("mark", open.Pop());
                    break;
            }
        }

        Assert.Empty(open);
    }

    [Fact]
    public void ALinkIsInTheStructureAndDescribed()
    {
        string pdf = Body(stack =>
        {
            stack.Add().Text(text =>
            {
                text.Run("See ");
                text.Link("the site", "https://example.com/");
            });
            stack.Add().Anchor("notes").Text("Notes");
            stack.Add().CrossReference("notes").Text("Go");
        });

        // Links take their keys as they are drawn, the page its own once it ends.
        Assert.Equal(
            ["0", "1"],
            Regex.Matches(pdf, @"/Subtype\s*/Link[\s\S]*?/StructParent (\d+)").Cast<Match>().Select(match => match.Groups[1].Value));
        Assert.Matches(@"/Contents\s*\(https://example\.com/\)", pdf);
        Assert.Matches(@"/Contents\s*\(notes\)", pdf);
        Assert.Equal(2, Regex.Matches(pdf, @"/Type\s*/OBJR").Count);
        Assert.Equal(2, Regex.Matches(pdf, @"/S\s*/Link\b").Count);
        Assert.Matches(@"/Link<</MCID \d+>>BDC", pdf);
        Assert.Matches(@"/ParentTreeNextKey 3\b", pdf);
    }

    [Fact]
    public void ALinkInDecorationStillBelongsToTheStructure()
    {
        string pdf = Export(section =>
        {
            section.RunningFoot().Link("https://example.com/").Text("Site");
            section.Body().Text("Body");
        });

        Assert.Matches(@"/StructParent \d+", pdf);
        Assert.Single(Regex.Matches(pdf, @"/Type\s*/OBJR"));

        string content = Assert.Single(Contents(pdf));
        int[] texts = Regex.Matches(content, @"^BT$", RegexOptions.Multiline).Cast<Match>().Select(match => match.Index).ToArray();
        Assert.Equal(["P", "Artifact"], texts.Select(index => MarkedAt(content, index)));
    }

    [Fact]
    public void AnElementGoingOnToAnotherPageSaysWhereItsContentIs()
    {
        string pdf = Body(stack => stack.Add().Text(text =>
        {
            for (int line = 1; line <= 40; line++)
                text.Line("Line " + line);
        }));

        Assert.True(Contents(pdf).Count > 1);
        Assert.Matches(@"/Type\s*/MCR\s*/Pg \d+ 0 R\s*/MCID 0", pdf);
        Assert.Single(Regex.Matches(pdf, @"/S\s*/P\b"));
        Assert.Matches(@"/StructParents 1\b", pdf);
    }

    [Fact]
    public void TheRunningHeadIsDecoration()
    {
        string pdf = Export(section =>
        {
            section.RunningHead().Text("Head");
            section.Body().Text("Body");
        });

        string content = Assert.Single(Contents(pdf));
        int[] texts = Regex.Matches(content, @"^BT$", RegexOptions.Multiline).Cast<Match>().Select(match => match.Index).ToArray();

        Assert.Equal(2, texts.Length);
        Assert.Equal("Artifact", MarkedAt(content, texts[0]));
        Assert.Equal("P", MarkedAt(content, texts[1]));
    }

    [Fact]
    public void ContentDrawnInDrawOrderIsMarkedAsItIsDrawn()
    {
        string pdf = Body(stack =>
        {
            stack.Add().DrawOrder(1).Tagged(ContentTag.Heading(1)).Text("Above");
            stack.Add().Text("Below");
        });

        string content = Assert.Single(Contents(pdf));
        Assert.True(content.IndexOf("/P<<", StringComparison.Ordinal) < content.IndexOf("/H1<<", StringComparison.Ordinal));
        Assert.Matches(@"/S\s*/H1", pdf);
    }

    [Fact]
    public void PdfUAShowsTheTitleAndClaimsThePart()
    {
        string pdf = Body(stack => stack.Add().Text("Hello"), new PdfExportOptions { Accessibility = PdfUAConformance.PdfUA1 });

        Assert.Matches(@"/ViewerPreferences\s*<<\s*/DisplayDocTitle\s+true\s*>>", pdf);
        Assert.Contains("<pdfuaid:part>1</pdfuaid:part>", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("pdfaid:part", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("pdfaExtension", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("OutputIntents", pdf, StringComparison.Ordinal);
        Assert.Matches(@"/Lang\s*\(en\)", pdf);
        Assert.Matches(@"/StructTreeRoot \d+ 0 R", pdf);
    }

    [Theory]
    [InlineData(null, "en")]
    [InlineData("Titled", null)]
    [InlineData(" ", "en")]
    [InlineData("Titled", " ")]
    public void PdfUANeedsATitleAndALanguage(string? title, string? language)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Export(
            section => section.Body().Text("Hello"),
            new PdfExportOptions { Accessibility = PdfUAConformance.PdfUA1 },
            info =>
            {
                info.Title = title;
                info.Language = language;
            }));

        Assert.Contains("PDF/UA", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PdfUANeedsEveryGlyph()
    {
        Document document = Document.Compose(composition => composition.Section(section =>
            section.Body().Text(text => text.Run("").Typeface(TestFonts.Sans))));
        document.Info.Title = "Missing";
        document.Info.Language = "en";

        Assert.Throws<MissingGlyphException>(() => document.ExportPdf(new PdfExportOptions
        {
            Accessibility = PdfUAConformance.PdfUA1,
            Typefaces = TestFonts.NewLibrary(includeInstalled: false),
        }));
    }

    [Theory]
    [InlineData(PdfAConformance.PdfA2A, "2")]
    [InlineData(PdfAConformance.PdfA3A, "3")]
    public void TheAccessibleLevelsOfPdfAAreTagged(PdfAConformance conformance, string part)
    {
        string pdf = Body(stack => stack.Add().Text("Hello"), new PdfExportOptions { Conformance = conformance });

        Assert.Contains($"<pdfaid:part>{part}</pdfaid:part>", pdf, StringComparison.Ordinal);
        Assert.Contains("<pdfaid:conformance>A</pdfaid:conformance>", pdf, StringComparison.Ordinal);
        Assert.Matches(@"/StructTreeRoot \d+ 0 R", pdf);
        Assert.Matches(@"/OutputIntents", pdf);
        Assert.DoesNotContain("<pdfuaid:part>", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void PdfAAndPdfUATogetherDescribeThePdfUASchema()
    {
        string pdf = Body(
            stack => stack.Add().Text("Hello"),
            new PdfExportOptions { Conformance = PdfAConformance.PdfA2A, Accessibility = PdfUAConformance.PdfUA1 });

        Assert.Contains("<pdfuaid:part>1</pdfuaid:part>", pdf, StringComparison.Ordinal);
        Assert.Contains("<pdfaSchema:prefix>pdfuaid</pdfaSchema:prefix>", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void ShadowsAreSmoothedExceptUnderPdfA()
    {
        void Shadowed(StackComposer stack) => stack.Add().Height(20).DropShadow(Ink.Rgb(0, 0, 0), 4).Blank();

        string plain = Body(Shadowed, new PdfExportOptions());
        string archived = Body(Shadowed, new PdfExportOptions { Conformance = PdfAConformance.PdfA2B });

        Assert.Equal(2, Regex.Matches(plain, @"/Interpolate\s+true").Count);
        Assert.DoesNotContain("/Interpolate", archived, StringComparison.Ordinal);
    }
}
