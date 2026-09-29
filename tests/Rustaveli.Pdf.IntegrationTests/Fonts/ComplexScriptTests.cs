using Rustaveli.Pdf.Fonts;
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

        // The cluster keeps the order HarfBuzz draws it in, right to left: what sits on the letter, then the letter.
        Assert.Equal([false, false, true], marked.Select(glyph => glyph.Advance > 0));
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
    public void QuotationMarksInRightToLeftArabicAreMirroredOnce()
    {
        // Noto Sans Arabic has both guillemets, so the quote and its marks are one run, shaped by HarfBuzz. Displayed
        // right to left each mark is its mirror image, as the core sets them: the » that closes the quote, read
        // last, is displayed first as «, and the « that opens it last as ».
        OpenTypeFont face = Shaped.Shaper.Resolve(Arabic);
        List<ShapedGlyph> core = Shape(Plain, "«سلام»", Arabic, rightToLeft: true);
        List<ShapedGlyph> shaped = Shape(Shaped, "«سلام»", Arabic, rightToLeft: true);

        Assert.Equal([face.GetGlyphId('«'), face.GetGlyphId('»')], [core[0].Glyph, core[^1].Glyph]);
        Assert.Equal([face.GetGlyphId('«'), face.GetGlyphId('»')], [shaped[0].Glyph, shaped[^1].Glyph]);

        // Each reads as the mark it is drawn as, as the core's do.
        Assert.Equal([core[0].ReadsAs, core[^1].ReadsAs], [shaped[0].ReadsAs, shaped[^1].ReadsAs]);
    }

    [Fact]
    public void AControlCharacterInAShapedRunIsSetAsNothing()
    {
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(Shaped.Shaper);
        List<ShapedGlyph> glyphs = Shape(Shaped, Salaam + "\u0001", Arabic);

        Assert.Equal(4, glyphs.Count);
        Assert.DoesNotContain(glyphs, glyph => glyph.Glyph == 0);
        Assert.Equal(Salaam + "\u0001", string.Concat(glyphs.Select(glyph => glyph.ReadsAs)));
        Assert.Equal(measurer.MeasureWidth(Salaam, Arabic), measurer.MeasureWidth(Salaam + "\u0001", Arabic), 0.001f);
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

    /// <summary>The first and last character of every block whose scripts HarfBuzz shapes.</summary>
    public static TheoryData<int> BlockEdges => new TheoryData<int>
    {
        0x0590, 0x05FF, 0x0600, 0x08FF, 0x0900, 0x0DFF, 0x0E00, 0x0FFF, 0x1000, 0x109F, 0x1780, 0x18AF, 0x1900, 0x1AAF,
        0x1B00, 0x1C4F, 0xA800, 0xA82F, 0xA840, 0xA8FF, 0xA900, 0xAAFF, 0xABC0, 0xABFF, 0xFB1D, 0xFB4F, 0xFB50, 0xFDFF,
        0xFE70, 0xFEFF,
    };

    [Theory]
    [MemberData(nameof(BlockEdges))]
    public void EveryBlockIsComplexToItsEdges(int character) =>
        Assert.True(ComplexScriptCharacters.Contains((char)character), $"U+{character:X4}");

    [Theory]
    [InlineData(0x058F)]
    [InlineData(0x10A0)]
    [InlineData(0x177F)]
    [InlineData(0x18B0)]
    [InlineData(0x1AB0)]
    [InlineData(0x1C50)]
    [InlineData(0xA7FF)]
    [InlineData(0xA830)]
    [InlineData(0xAB00)]
    [InlineData(0xABBF)]
    [InlineData(0xAC00)]
    [InlineData(0xFB1C)]
    [InlineData(0xFE00)]
    [InlineData(0xFF00)]
    public void CharactersJustOutsideTheBlocksAreNot(int character) =>
        Assert.False(ComplexScriptCharacters.Contains((char)character), $"U+{character:X4}");
}
