using Rustaveli.Pdf.Shaping;
using Rustaveli.Pdf.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Rustaveli.Pdf.IntegrationTests.Fonts;

/// <summary>
/// Complex scripts shaped by the Shaping package, on the committed Noto Sans Arabic and Devanagari, each compared
/// with the same text set without it, a character at a time.
/// </summary>
public class ComplexScriptTests
{
    private const string Salaam = "سلام";

    private static readonly TypeStyle Arabic = TypeStyle.Default.WithTypeface("Noto Sans Arabic").WithPointSize(20);
    private static readonly TypeStyle Devanagari = TypeStyle.Default.WithTypeface("Noto Sans Devanagari").WithPointSize(20);

    private static readonly TypefaceLibrary Plain = Library();
    private static readonly TypefaceLibrary Shaped = Library().ShapeComplexScripts();

    private static TypefaceLibrary Library()
    {
        TypefaceLibrary library = TestFonts.NewLibrary(includeInstalled: false);
        library.RegisterFile(FontAssets.PathOf("NotoSansArabic-Regular.ttf"));
        library.RegisterFile(FontAssets.PathOf("NotoSansDevanagari-Regular.ttf"));
        return library;
    }

    private static List<ShapedGlyph> Shape(TypefaceLibrary library, string text, TypeStyle style, bool rightToLeft = false)
    {
        List<ShapedGlyph> glyphs = [];

        foreach (ShapedGlyph glyph in library.Shaper.Walk(text.AsSpan(), style, rightToLeft))
            glyphs.Add(glyph);

        return glyphs;
    }

    [Fact]
    public void ArabicLettersTakeTheirJoiningForms()
    {
        List<ShapedGlyph> isolated = Shape(Plain, Salaam, Arabic);
        List<ShapedGlyph> joined = Shape(Shaped, Salaam, Arabic);

        // Seen opens the word, lam joins on both sides and alef closes the join: initial, medial and final forms.
        Assert.Equal(4, joined.Count);

        for (int letter = 0; letter < 3; letter++)
            Assert.NotEqual(isolated[letter].Glyph, joined[letter].Glyph);
    }

    [Fact]
    public void AStylesFeaturesReachHarfBuzz()
    {
        // With initial forms turned off, the seen opening the word keeps its isolated form.
        List<ShapedGlyph> isolated = Shape(Plain, Salaam, Arabic);
        List<ShapedGlyph> withoutInitials = Shape(Shaped, Salaam, Arabic.WithFeature("init", 0));

        Assert.Equal(isolated[0].Glyph, withoutInitials[0].Glyph);
        Assert.NotEqual(isolated[1].Glyph, withoutInitials[1].Glyph);
    }

    [Fact]
    public void ShapedArabicStillReadsAsItsCharacters()
    {
        List<ShapedGlyph> joined = Shape(Shaped, Salaam, Arabic);

        Assert.Equal(Salaam, string.Concat(joined.Select(glyph => glyph.ReadsAs)));
        Assert.Equal([0, 1, 2, 3], joined.Select(glyph => glyph.Start));
    }

    [Fact]
    public void AMarkIsPlacedOnItsLetterWithoutMovingThePen()
    {
        // Beh with a fatha above it. Noto Sans Arabic draws the letter as a skeleton and a dot, so the cluster is three
        // glyphs; the fatha and the dot take no room of their own and are drawn where the font anchors them.
        List<ShapedGlyph> plain = Shape(Shaped, "ب", Arabic);
        List<ShapedGlyph> marked = Shape(Shaped, "بَ", Arabic);

        Assert.Equal(["بَ", string.Empty, string.Empty], marked.Select(glyph => glyph.ReadsAs));
        Assert.Contains(marked, glyph => glyph.Advance == 0 && (glyph.XOffset != 0 || glyph.YOffset != 0));
        Assert.Equal(plain.Sum(glyph => glyph.Advance), marked.Sum(glyph => glyph.Advance), 0.001f);
    }

    [Fact]
    public void RightToLeftArabicIsDisplayedFromItsEnd()
    {
        List<ShapedGlyph> logical = Shape(Shaped, Salaam, Arabic);
        List<ShapedGlyph> displayed = Shape(Shaped, Salaam, Arabic, rightToLeft: true);

        Assert.Equal(logical.Select(glyph => glyph.Glyph).Reverse(), displayed.Select(glyph => glyph.Glyph));
    }

    [Fact]
    public void ADevanagariVowelSignIsDrawnBeforeTheConsonantItFollows()
    {
        // Ka followed by the vowel sign i: typed after the consonant, the sign is written before it.
        List<ShapedGlyph> typed = Shape(Plain, "कि", Devanagari);
        List<ShapedGlyph> shaped = Shape(Shaped, "कि", Devanagari);

        // Unshaped, ka comes first; shaped, the sign does, in the form that fits over the consonant.
        Assert.Equal(2, shaped.Count);
        Assert.NotEqual(typed[0].Glyph, shaped[0].Glyph);
        Assert.NotEqual(typed[1].Glyph, shaped[1].Glyph);

        // The two glyphs are one cluster: the first reads as both characters, the second as none.
        Assert.Equal(["कि", string.Empty], shaped.Select(glyph => glyph.ReadsAs));
    }

    [Fact]
    public void TextInOtherScriptsIsSetAsBefore()
    {
        TypeStyle sans = TypeStyle.Default.WithTypeface(TestFonts.Sans);

        Assert.Equal(
            Shape(Plain, "office 123", sans).Select(glyph => (glyph.Glyph, glyph.Advance, glyph.Kerning)),
            Shape(Shaped, "office 123", sans).Select(glyph => (glyph.Glyph, glyph.Advance, glyph.Kerning)));
    }

    [Fact]
    public void MeasuringFollowsTheShapedGlyphs()
    {
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(Shaped.Shaper);
        float advances = Shape(Shaped, Salaam, Arabic).Sum(glyph => glyph.Advance);

        Assert.Equal(advances, measurer.MeasureWidth(Salaam, Arabic), 0.001f);
        Assert.NotEqual(new OpenTypeMeasurer(Plain.Shaper).MeasureWidth(Salaam, Arabic), measurer.MeasureWidth(Salaam, Arabic));
    }

    [Fact]
    public void ShapedArabicReadsBackFromThePdf()
    {
        byte[] pdf = Document.Compose(composition => composition.Section(section =>
        {
            section.DefaultType = Arabic;
            section.Body().RightToLeft().Text(Salaam);
        })).ExportPdf(new PdfExportOptions { Typefaces = Shaped });

        using PdfDocument parsed = PdfDocument.Open(pdf);
        Page page = parsed.GetPage(1);

        // Glyphs are drawn left to right, the word's end first; read from the right, they spell the word.
        Assert.Equal(Salaam, string.Concat(page.Letters.Reverse().Select(letter => letter.Value)));
    }

    [Fact]
    public void TurningComplexScriptsOnTwiceKeepsOneShaper()
    {
        TypefaceLibrary library = Library().ShapeComplexScripts();
        IComplexShaper? shaper = library.ComplexShaper;

        Assert.Same(library, library.ShapeComplexScripts());
        Assert.Same(shaper, library.ComplexShaper);
        Assert.IsType<HarfBuzzShaper>(shaper);
    }

    [Fact]
    public void ALibraryIsRequired() =>
        Assert.Equal("library", Assert.Throws<ArgumentNullException>(() => ComplexScripts.ShapeComplexScripts(null!)).ParamName);

    [Theory]
    [InlineData('֐', true)]
    [InlineData('ا', true)]
    [InlineData('क', true)]
    [InlineData('ก', true)]
    [InlineData('က', true)]
    [InlineData('ក', true)]
    [InlineData('יִ', true)]
    [InlineData('﻿', true)]
    [InlineData('֏', false)]
    [InlineData('A', false)]
    [InlineData('ა', false)]
    [InlineData('世', false)]
    public void RunsInComplexScriptsAreHandedToHarfBuzz(char character, bool complex)
    {
        HarfBuzzShaper shaper = new HarfBuzzShaper();

        Assert.Equal(complex, shaper.Handles($"ab{character}".AsSpan()));
    }
}
