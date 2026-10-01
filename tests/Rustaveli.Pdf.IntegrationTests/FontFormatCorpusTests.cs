using System.Text.RegularExpressions;
using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.IntegrationTests.Fonts;
using Rustaveli.Pdf.Text;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Fonts in the formats the Noto files never exercise — a variable TrueType font, a variable CFF2 font, a TrueType
/// collection and a font kerned by the older <c>kern</c> table — registered, measured, set, embedded and drawn.
/// </summary>
/// <remarks>The fonts, where they come from and how they were made are listed in tests/assets/fonts/corpus.</remarks>
public class FontFormatCorpusTests
{
    private const string VariableTrueType = "SourceSans3VF-Upright.ttf";
    private const string VariableCff2 = "SourceSans3VF-Upright.otf";
    private const string Collection = "NotoSans-Collection.ttc";
    private const string KernTable = "KernTableTest-Regular.ttf";

    private const string SourceSans = "SourceSans3VF";
    private const string KernTableFamily = "Kern Table Test";

    // Kerned pairs, and the ligatures "fi" and "ffi", whose glyphs must read back as the letters they stand for.
    private const string Sentence = "Sphinx of black quartz, judge my vow: AVATAR Today, fifty offices, 1234.";

    private static string PathOf(string file) => FontAssets.PathOf(Path.Combine("corpus", file));

    private static TypefaceLibrary LibraryWith(params string[] files)
    {
        TypefaceLibrary library = new TypefaceLibrary(includeInstalled: false);

        foreach (string file in files)
            library.RegisterFile(PathOf(file));

        return library;
    }

    private static Document Paragraph(string text, TypeStyle style) => Document.Compose(composition => composition.Section(section =>
    {
        // Wide enough for the sentence on one line at 12 points, so the text reads back without a line break in it.
        section.Trim = new Extent(600, 120);
        section.Margins = Sides.All(20);
        section.DefaultType = style;
        section.Body().Text(text);
    }));

    private static ImageExportOptions ImagesWith(TypefaceLibrary library) =>
        new ImageExportOptions { Resolution = 72, Format = PageImageFormat.Png, Typefaces = library };

    private static PdfDocument ExportAndOpen(Document document, TypefaceLibrary library) =>
        PdfDocument.Open(document.ExportPdf(new PdfExportOptions { Typefaces = library }));

    private static void AssertSetIn(Page page, string postScriptName)
    {
        // A subset's name is the face's PostScript name behind a tag of six capital letters.
        Assert.All(page.Letters, letter => Assert.Matches("^[A-Z]{6}\\+" + Regex.Escape(postScriptName) + "$", letter.FontName));
    }

    [Theory]
    [InlineData(VariableTrueType, SourceSans, false, "SourceSans3VF-ExtraLight")]
    [InlineData(Collection, TestFonts.Sans, false, "NotoSans-Regular")]
    [InlineData(Collection, TestFonts.Sans, true, "NotoSans-Bold")]
    [InlineData(KernTable, KernTableFamily, false, "KernTableTest-Regular")]
    public void TextSetInEachFormatReadsBackAsWrittenInThatFace(string file, string typeface, bool bold, string postScriptName)
    {
        TypefaceLibrary library = LibraryWith(file);
        TypeStyle style = TypeStyle.Default.WithTypeface(typeface).WithPointSize(12);

        using PdfDocument parsed = ExportAndOpen(Paragraph(Sentence, bold ? style.Bold() : style), library);

        Assert.Equal(Sentence, parsed.GetPage(1).Text);
        AssertSetIn(parsed.GetPage(1), postScriptName);
    }

    [Theory]
    [InlineData(VariableTrueType, SourceSans)]
    [InlineData(Collection, TestFonts.Sans)]
    [InlineData(KernTable, KernTableFamily)]
    public void EachFormatMeasuresTextByItsOwnAdvances(string file, string typeface)
    {
        TypefaceLibrary library = LibraryWith(file);
        TypeStyle style = TypeStyle.Default.WithTypeface(typeface).WithPointSize(10).WithFeature("kern", 0);
        OpenTypeFont face = library.Shaper.Resolve(style);

        float expected = 0;
        foreach (char character in "Hamburg")
            expected += face.GetAdvance(face.GetGlyphId(character), 10);

        Assert.True(face.GetGlyphId('H') != 0, "The face must have the letters measured.");
        Assert.Equal(expected, new OpenTypeMeasurer(library.Shaper).MeasureWidth("Hamburg", style), 3);
        Assert.True(new OpenTypeMeasurer(library.Shaper).GetMetrics(style).Ascent > 0);
    }

    [Theory]
    [InlineData(VariableTrueType, SourceSans, false)]
    [InlineData(VariableCff2, SourceSans, false)]
    [InlineData(Collection, TestFonts.Sans, false)]
    [InlineData(Collection, TestFonts.Sans, true)]
    [InlineData(KernTable, KernTableFamily, false)]
    public void EachFormatExportsAsAPageImageWithTheTextDrawn(string file, string typeface, bool bold)
    {
        TypefaceLibrary library = LibraryWith(file);
        TypeStyle style = TypeStyle.Default.WithTypeface(typeface).WithPointSize(16);
        Document document = Paragraph(Sentence, bold ? style.Bold() : style);

        IReadOnlyList<byte[]> pages = document.ExportImages(ImagesWith(library));

        using SKBitmap page = SKBitmap.Decode(Assert.Single(pages));
        Assert.Equal((600, 120), (page.Width, page.Height));
        Assert.Contains(page.Pixels, pixel => pixel.Red < 128);
    }

    [Fact]
    public void EveryFaceOfACollectionIsRegisteredAndChosenByStyleOrByName()
    {
        TypefaceLibrary library = LibraryWith(Collection);
        TypeStyle style = TypeStyle.Default.WithTypeface(TestFonts.Sans);

        OpenTypeFont regular = library.Shaper.Resolve(style);
        OpenTypeFont bold = library.Shaper.Resolve(style.Bold());

        Assert.Equal((0, "NotoSans-Regular"), (regular.FaceIndex, regular.Names.PostScriptName));
        Assert.Equal((1, "NotoSans-Bold"), (bold.FaceIndex, bold.Names.PostScriptName));

        // A face's own PostScript name picks it out of the collection whatever the style asks for.
        Assert.Equal(1, library.Shaper.Resolve(TypeStyle.Default.WithTypeface("NotoSans-Bold")).FaceIndex);

        // Both faces are read from one copy of the file.
        Assert.True(regular.FileData.Equals(bold.FileData), "The faces of a collection share the file they were read from.");
    }

    [Fact]
    public void BothFacesOfACollectionSetTextInOneDocument()
    {
        TypefaceLibrary library = LibraryWith(Collection);
        Document document = Document.Compose(composition => composition.Section(section =>
        {
            section.DefaultType = TypeStyle.Default.WithTypeface(TestFonts.Sans);
            section.Body().Text(text =>
            {
                text.Run("Regular ");
                text.Run("Bold").Bold();
            });
        }));

        using PdfDocument parsed = ExportAndOpen(document, library);
        IReadOnlyList<Letter> letters = parsed.GetPage(1).Letters;

        Assert.Equal("Regular Bold", parsed.GetPage(1).Text);
        Assert.EndsWith("+NotoSans-Regular", letters[0].FontName, StringComparison.Ordinal);
        Assert.EndsWith("+NotoSans-Bold", letters[^1].FontName, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBoldFaceOfACollectionIsDrawnInItsOwnWeight()
    {
        TypefaceLibrary library = LibraryWith(Collection);
        TypeStyle style = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(16);

        int Ink(TypeStyle drawn)
        {
            using SKBitmap page = SKBitmap.Decode(Paragraph(Sentence, drawn).ExportImages(ImagesWith(library))[0]);
            return page.Pixels.Count(pixel => pixel.Red < 128);
        }

        // Drawn from the regular face, the bold text would cover no more of the page than the regular.
        Assert.True(Ink(style.Bold()) > Ink(style) * 1.2, "The bold face must be drawn heavier than the regular.");
    }

    [Fact]
    public void AFolderHoldingEveryFormatIsSearchedLikeRegisteredFonts()
    {
        TypefaceLibrary library = new TypefaceLibrary(includeInstalled: false);
        library.SearchFolder(Path.GetDirectoryName(PathOf(Collection))!);
        TypeStyle style = TypeStyle.Default;

        Assert.Equal("SourceSans3VF-ExtraLight", library.Shaper.Resolve(style.WithTypeface(SourceSans)).Names.PostScriptName);
        Assert.Equal("NotoSans-Bold", library.Shaper.Resolve(style.WithTypeface(TestFonts.Sans).Bold()).Names.PostScriptName);
        Assert.IsType<LegacyKerningTable>(library.Shaper.Resolve(style.WithTypeface(KernTableFamily)).Kerning);
    }

    [Fact]
    public void AKernTableFontIsKernedFromItsKernTable()
    {
        OpenTypeFont face = LibraryWith(KernTable).Shaper.Resolve(TypeStyle.Default.WithTypeface(KernTableFamily));

        Assert.IsType<LegacyKerningTable>(face.Kerning);
        Assert.Equal(-80, face.GetKerning(face.GetGlyphId('A'), face.GetGlyphId('V')));
        Assert.Equal(-150, face.GetKerning(face.GetGlyphId('P'), face.GetGlyphId('.')));
    }

    [Theory]
    [InlineData("AV", -80)]
    [InlineData("To", -90)]
    [InlineData("Yo", -100)]
    [InlineData("LT", -110)]
    [InlineData("P.", -150)]
    [InlineData("AW", 0)]
    public void APairInTheKernTableMeasuresNarrowerThanTheSameLettersUnkerned(string pair, int adjustment)
    {
        // AW is kerned by Noto Sans's GPOS but is not in the kern table, which is all this font kerns by.
        TypefaceLibrary library = LibraryWith(KernTable);
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(library.Shaper);
        TypeStyle kerned = TypeStyle.Default.WithTypeface(KernTableFamily).WithPointSize(100);

        float difference = measurer.MeasureWidth(pair, kerned) - measurer.MeasureWidth(pair, kerned.WithFeature("kern", 0));

        // At 100 points a font unit of a 1000-unit em is a tenth of a point.
        Assert.Equal(adjustment / 10f, difference, 3);
    }

    [Fact]
    public void AKernTablePairIsSetCloserInThePdf()
    {
        TypefaceLibrary library = LibraryWith(KernTable);
        TypeStyle style = TypeStyle.Default.WithTypeface(KernTableFamily).WithPointSize(50);

        using PdfDocument kerned = ExportAndOpen(Paragraph("AV", style), library);
        using PdfDocument unkerned = ExportAndOpen(Paragraph("AV", style.WithFeature("kern", 0)), library);

        double Gap(PdfDocument parsed) =>
            parsed.GetPage(1).Letters[1].StartBaseLine.X - parsed.GetPage(1).Letters[0].StartBaseLine.X;

        Assert.Equal(-4, Gap(kerned) - Gap(unkerned), 2);
        Assert.Equal("AV", kerned.GetPage(1).Text);
        AssertSetIn(kerned.GetPage(1), "KernTableTest-Regular");
    }

    [Fact]
    public void AVariableTrueTypeFontIsSetInItsDefaultInstanceAtEveryWeight()
    {
        // Variations are not applied: the font's default instance is its one face, ExtraLight here, and a weight the
        // family lacks is set in the nearest it has, as for any other family.
        TypefaceLibrary library = LibraryWith(VariableTrueType);
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(library.Shaper);
        TypeStyle style = TypeStyle.Default.WithTypeface(SourceSans);

        OpenTypeFont face = library.Shaper.Resolve(style);

        Assert.Equal(OutlineFormat.TrueType, face.Outlines);
        Assert.Equal(200, face.Style.Weight);
        Assert.True(face.GetKerning(face.GetGlyphId('A'), face.GetGlyphId('V')) < 0, "The default instance keeps its GPOS kerning.");
        Assert.Same(face, library.Shaper.Resolve(style.Bold()));
        Assert.Same(face, library.Shaper.Resolve(style.WithWeight(TypeWeight.Black)));
        Assert.Equal(measurer.MeasureWidth("Hamburg", style), measurer.MeasureWidth("Hamburg", style.Bold()));
    }

    [Fact]
    public void ACff2FontIsRegisteredButPassedOverForTheBundledFace()
    {
        // PDF has no font program type for CFF2, so the face is never chosen to set text: the request falls through to
        // the substitutes, and with nothing else registered or installed, to the bundled Noto Sans.
        TypefaceLibrary library = LibraryWith(VariableCff2);
        TypeStyle style = TypeStyle.Default.WithTypeface(SourceSans);

        Assert.Equal("NotoSans-Regular", library.Shaper.Resolve(style).Names.PostScriptName);

        using PdfDocument parsed = ExportAndOpen(Paragraph(Sentence, style), library);

        Assert.Equal(Sentence, parsed.GetPage(1).Text);
        AssertSetIn(parsed.GetPage(1), "NotoSans-Regular");
    }

    [Theory]
    [InlineData(VariableCff2, VariableTrueType)]
    [InlineData(VariableTrueType, VariableCff2)]
    public void ACff2FontIsPassedOverForATrueTypeFaceOfTheSameFamily(string first, string second)
    {
        // Both builds of Source Sans 3 have the same family name and weight, as they would when both are installed.
        // The kern-table font, registered first, is what any registered face would mean, and must not stand in.
        TypefaceLibrary library = LibraryWith(KernTable, first, second);
        TypeStyle style = TypeStyle.Default.WithTypeface(SourceSans);

        OpenTypeFont face = library.Shaper.Resolve(style);

        Assert.Equal(OutlineFormat.TrueType, face.Outlines);

        using PdfDocument parsed = ExportAndOpen(Paragraph(Sentence, style), library);

        AssertSetIn(parsed.GetPage(1), "SourceSans3VF-ExtraLight");
    }
}
