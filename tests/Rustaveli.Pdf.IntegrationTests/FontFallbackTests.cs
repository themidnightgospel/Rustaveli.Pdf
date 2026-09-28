using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Rustaveli.Pdf.IntegrationTests;

/// <summary>
/// Verifies that characters absent from the requested font are drawn with one that has them.
/// </summary>
/// <remarks>
/// The decisive check is extraction: a glyph the primary font lacks is emitted as .notdef without fallback, and
/// a reader recovers nothing for it. Getting the characters back out proves a covering face was embedded.
/// </remarks>
public class FontFallbackTests
{
    private const string Latin = "Hello";
    private const string Cjk = "世界";
    private const string Georgian = "გამარჯობა";

    private static readonly TypeStyle Sans = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(16);

    private static OpenTypeMeasurer Measurer(TypefaceLibrary library) => new OpenTypeMeasurer(library.Shaper);

    private static Document Build(string text) => Build(text, Sans);

    private static Document Build(string text, TypeStyle style) =>
        Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = PaperSizes.A4;
            section.Margins = Sides.All(30);
            section.DefaultType = style;
            section.Body().Text(text);
        }));

    /// <summary>The embedded font a character was drawn with, without the subset tag.</summary>
    private static string FontOf(byte[] pdf, string character)
    {
        using PdfDocument parsed = PdfDocument.Open(pdf);
        string? name = Assert.Single(parsed.GetPage(1).Letters, letter => letter.Value == character).FontName;

        Assert.False(string.IsNullOrEmpty(name), $"'{character}' was drawn with an unnamed font.");
        return name!.Substring(name.IndexOf('+') + 1);
    }

    /// <summary>
    /// Finds a character the test family lacks, together with an installed family that has it but is not the one
    /// the library would pick by itself — the pair a test needs to tell an explicit fallback apart from the
    /// automatic choice. Several scripts are tried so that some pair exists on any host with fonts.
    /// </summary>
    private static bool TryFindAlternativeFallback(out string character, out string family)
    {
        int[] codepoints = [0x4E16, 0x2605, 0x10D0, 0x0E01, 0x0905];
        TypeShaper shaper = TypefaceLibrary.Shared.Shaper;
        OpenTypeFont primary = shaper.Resolve(Sans);

        foreach (int codepoint in codepoints)
        {
            if (primary.HasGlyph(codepoint))
                continue;

            string automatic = shaper.FaceFor(primary, new FontRequest(TestFonts.Sans), TypefaceFallbacks.None, codepoint).Names.PreferredFamily;

            FontFaceInfo? alternative = SystemFontIndex.Current.Faces
                .Where(face => face.IsEmbeddable && face.Style.Slant == FontSlant.Upright)
                .Where(face => face.Names.PreferredFamily != automatic && face.Names.PreferredFamily != TestFonts.Sans)
                .OrderBy(face => face.Names.PreferredFamily, StringComparer.Ordinal)
                .FirstOrDefault(face => face.Covers(codepoint));

            if (alternative is not null)
            {
                character = char.ConvertFromUtf32(codepoint);
                family = alternative.Names.PreferredFamily;
                return true;
            }
        }

        character = string.Empty;
        family = string.Empty;
        return false;
    }

    [Fact]
    public void RendersCharactersTheRequestedFontDoesNotHave()
    {
        using PdfDocument parsed = PdfDocument.Open(Build($"{Latin} {Cjk}").ExportPdf());
        Page page = parsed.GetPage(1);

        // The CJK face is the platform's, so a failure names it.
        string faces = string.Join(", ", page.Letters.Select(letter => $"{letter.Value}={letter.FontName}"));
        Assert.True(page.Text.Contains(Latin), $"The Latin text did not survive. Letters: {faces}");
        Assert.True(page.Text.Contains(Cjk), $"The CJK text did not survive. Letters: {faces}");
    }

    [Fact]
    public void MixedScriptTextMeasuresWiderThanItsLatinPartAlone()
    {
        OpenTypeMeasurer measurer = Measurer(TypefaceLibrary.Shared);

        float latinOnly = measurer.MeasureWidth(Latin, Sans);
        float mixed = measurer.MeasureWidth($"{Latin}{Cjk}", Sans);

        Assert.True(mixed > latinOnly, $"Mixed-script text measured {mixed} against {latinOnly} for the Latin part alone.");
    }

    [Fact]
    public void MeasuredWidthMatchesWhatIsDrawn()
    {
        // Measurement and drawing must split the string identically; if they disagreed, a fallback glyph would
        // land somewhere other than where its advance was reserved. Kerning never crosses faces, so the parts sum.
        OpenTypeMeasurer measurer = Measurer(TypefaceLibrary.Shared);

        float whole = measurer.MeasureWidth($"{Latin}{Cjk}", Sans);
        float parts = measurer.MeasureWidth(Latin, Sans) + measurer.MeasureWidth(Cjk, Sans);

        Assert.Equal(parts, whole, 0.01f);
    }

    [Fact]
    public void PurelyLatinTextIsUnaffected()
    {
        OpenTypeMeasurer measurer = Measurer(TypefaceLibrary.Shared);
        OpenTypeFont primary = TypefaceLibrary.Shared.Shaper.Resolve(Sans);

        // Latin the test family covers is set in it alone, and measures as the font itself measures it.
        Assert.Equal(primary.MeasureWidth(Latin.AsSpan(), Sans.PointSize), measurer.MeasureWidth(Latin, Sans), 0.001f);
    }

    [Fact]
    public void AnExplicitFallbackIsPreferredOverTheAutomaticChoice()
    {
        Assert.True(
            TryFindAlternativeFallback(out string character, out string family),
            "No installed family covers a character Noto Sans lacks other than the automatic pick; the rendering suite requires system fonts.");

        TypefaceLibrary library = TestFonts.NewLibrary();
        library.Fallbacks = [family];

        string drawnDirectly = FontOf(Build(character, Sans.WithTypeface(family)).ExportPdf(), character);
        string drawnAsFallback = FontOf(Build($"A{character}").ExportPdf(new PdfExportOptions { Typefaces = library }), character);
        string automatic = FontOf(Build($"A{character}").ExportPdf(), character);

        Assert.Equal(drawnDirectly, drawnAsFallback);

        // Left to the shared library, which names no fallbacks, another face is chosen. That is what shows the
        // match above came from the explicit fallback, and from the library the options supplied.
        Assert.NotEqual(drawnDirectly, automatic);
    }

    /// <summary>
    /// A library of the committed faces alone: Noto Sans (registered first), Noto Sans Georgian, which has no Latin
    /// letters, and Specimen Sans, which has.
    /// </summary>
    private static TypefaceLibrary CommittedLibrary()
    {
        TypefaceLibrary library = TestFonts.NewLibrary(includeInstalled: false);
        library.RegisterFile(Fonts.FontAssets.PathOf("NotoSansGeorgian-Regular.ttf"));
        library.RegisterFile(Fonts.FontAssets.PathOf("SpecimenSans.ttc"));
        return library;
    }

    private static string FamilyOf(TypefaceLibrary library, string text, TypeStyle style)
    {
        foreach (ShapedGlyph glyph in library.Shaper.Walk(text.AsSpan(), style))
            return glyph.Face.Names.PreferredFamily;

        throw new InvalidOperationException("Nothing was set.");
    }

    [Fact]
    public void AStylesOwnFallbackIsTriedBeforeAnyOtherFace()
    {
        TypefaceLibrary library = CommittedLibrary();
        TypeStyle georgian = TypeStyle.Default.WithTypeface("Noto Sans Georgian");

        Assert.Equal(TestFonts.Sans, FamilyOf(library, "A", georgian));
        Assert.Equal("Specimen Sans", FamilyOf(library, "A", georgian.WithTypeface("Noto Sans Georgian", "Specimen Sans")));
    }

    [Fact]
    public void AStylesFallbacksAreTriedInOrder()
    {
        TypefaceLibrary library = CommittedLibrary();
        TypeStyle style = TypeStyle.Default.WithTypeface("Noto Sans Georgian", "Nobody Has This", "Specimen Sans", TestFonts.Sans);

        // The first fallback nobody has is passed over; the next that has the letter sets it.
        Assert.Equal("Specimen Sans", FamilyOf(library, "A", style));
    }

    [Fact]
    public void StylesWithDifferentFallbacksDoNotShareWhatTheyFound()
    {
        TypefaceLibrary library = CommittedLibrary();
        TypeStyle plain = TypeStyle.Default.WithTypeface("Noto Sans Georgian");

        // The plain style finds Noto Sans for A first; a style naming Specimen Sans must still get Specimen Sans.
        Assert.Equal(TestFonts.Sans, FamilyOf(library, "A", plain));
        Assert.Equal("Specimen Sans", FamilyOf(library, "A", plain.WithTypeface("Noto Sans Georgian", "Specimen Sans")));
        Assert.Equal(TestFonts.Sans, FamilyOf(library, "A", plain));
    }

    [Fact]
    public void ARunNamesItsFallbacksWithItsTypeface()
    {
        TypefaceLibrary library = CommittedLibrary();

        using PdfDocument parsed = PdfDocument.Open(Document.Compose(composition => composition.Section(section =>
            section.Body().Text(text => text.Run("აA").Typeface("Noto Sans Georgian", "Specimen Sans"))))
            .ExportPdf(new PdfExportOptions { Typefaces = library }));

        Page page = parsed.GetPage(1);

        Assert.Equal("აA", page.Text);
        Assert.Contains("SpecimenSans", page.Letters.Single(letter => letter.Value == "A").FontName);
    }

    [Fact]
    public void EachScriptFindsItsOwnFallback()
    {
        // The face found for the CJK characters has no Georgian, so the Georgian lookup must look past the
        // fallback already discovered for this style instead of settling for it. The CJK face is the platform's;
        // Georgian is registered, since not every platform has a font for it.
        TypefaceLibrary library = TestFonts.NewLibrary();
        library.RegisterFile(TestFonts.PathOf("NotoSansGeorgian-Regular.ttf"));
        OpenTypeMeasurer measurer = Measurer(library);

        float whole = measurer.MeasureWidth(Cjk + Georgian, Sans);
        float parts = measurer.MeasureWidth(Cjk, Sans) + measurer.MeasureWidth(Georgian, Sans);

        using PdfDocument parsed = PdfDocument.Open(Build($"{Cjk} {Georgian}").ExportPdf(new PdfExportOptions { Typefaces = library }));
        Page page = parsed.GetPage(1);

        // The faces found are the platform's, so a failure names them: which one each letter was set in.
        string faces = string.Join(", ", page.Letters.Select(letter => $"{letter.Value}={letter.FontName}"));
        Assert.Equal(parts, whole, 0.01f);
        Assert.True(page.Text.Contains(Cjk), $"The CJK text did not survive. Letters: {faces}");
        Assert.True(page.Text.Contains(Georgian), $"The Georgian text did not survive. Letters: {faces}");
    }

    [Fact]
    public void ItalicTextFallsBackToo()
    {
        TypefaceLibrary library = TestFonts.NewLibrary();

        using PdfDocument parsed = PdfDocument.Open(Build($"{Latin} {Cjk}", Sans.Italic()).ExportPdf(new PdfExportOptions { Typefaces = library }));
        string text = parsed.GetPage(1).Text;

        Assert.Contains(Latin, text);
        Assert.Contains(Cjk, text);
    }

    [Fact]
    public void ACharacterNoFontHasLeavesTheTextAroundItIntact()
    {
        // U+0378 is unassigned, so no face anywhere has it. The run must carry on rather than fail, and it must
        // still be measured with whatever it is drawn with.
        const string Unassigned = "͸";
        TypefaceLibrary library = TestFonts.NewLibrary();
        OpenTypeMeasurer measurer = Measurer(library);

        float whole = measurer.MeasureWidth($"A{Unassigned}B", Sans);
        float parts = measurer.MeasureWidth("A", Sans) + measurer.MeasureWidth(Unassigned, Sans) + measurer.MeasureWidth("B", Sans);

        using PdfDocument parsed = PdfDocument.Open(Build($"A{Unassigned}B").ExportPdf(new PdfExportOptions { Typefaces = library }));
        IReadOnlyList<Letter> letters = parsed.GetPage(1).Letters;

        Assert.Equal(parts, whole, 0.01f);
        Assert.Equal("A", letters[0].Value);
        Assert.Equal("B", letters[letters.Count - 1].Value);
    }
}
