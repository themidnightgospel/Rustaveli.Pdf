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
    public void FittingNeverSeparatesAVowelSignFromItsConsonant()
    {
        // Set a character at a time, ka and the vowel sign aa after it are two glyphs, each with an advance of its
        // own; a width that ends inside the sign still cannot keep the consonant without it.
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(Plain.Shaper);
        List<ShapedGlyph> glyphs = Shape(Plain, "का", Devanagari);

        Assert.Equal(2, glyphs.Count);
        Assert.Equal(0, measurer.MeasureCharactersFitting("का", Devanagari, glyphs[0].Advance + (glyphs[1].Advance / 2)));
        Assert.Equal(2, measurer.MeasureCharactersFitting("काका", Devanagari, measurer.MeasureWidth("का", Devanagari) + (glyphs[0].Advance / 2)));
    }

    [Fact]
    public void TrackingDoesNotPullJoinedLettersApart()
    {
        // Arabic is written joined, so tracking falls only between words: around the space, not between the letters.
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(Shaped.Shaper);
        TypeStyle tracked = Arabic.WithTracking(5);

        Assert.Equal(measurer.MeasureWidth(Salaam, Arabic), measurer.MeasureWidth(Salaam, tracked), 0.001f);
        Assert.Equal(measurer.MeasureWidth("بَ", Arabic), measurer.MeasureWidth("بَ", tracked), 0.001f);
        Assert.Equal(
            measurer.MeasureWidth(Salaam + " " + Salaam, Arabic) + 10,
            measurer.MeasureWidth(Salaam + " " + Salaam, tracked),
            0.001f);
    }

    [Fact]
    public void AMarkIsSetInTheFaceOfTheLetterItSitsOn()
    {
        // Noto Sans has no Devanagari, so ka comes from Noto Sans Devanagari. Both faces have the combining asterisk
        // above, and Noto Sans a zero width joiner: each goes with the ka, rather than to the style's own face.
        TypeStyle sans = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(20);
        OpenTypeFont devanagari = Plain.Shaper.Resolve(Devanagari);

        Assert.All(Shape(Plain, "क⃰", sans), glyph => Assert.Same(devanagari, glyph.Face));
        Assert.All(Shape(Plain, "क‍", sans), glyph => Assert.Same(devanagari, glyph.Face));

        // A mark the letter's face lacks is still set in a face that has it.
        Assert.Same(devanagari, Shape(Plain, "aा", sans)[1].Face);
    }

    [Fact]
    public void ALineIsTallEnoughForTheFallbackFacesSetOnIt()
    {
        // Noto Sans has no Arabic, which comes from Noto Sans Arabic, a face reaching further above and below.
        TypeStyle sans = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(20);

        List<double> Baselines(string text)
        {
            byte[] pdf = Document.Compose(composition => composition.Section(section =>
            {
                section.DefaultType = sans;
                section.Body().Text(text);
            })).ExportPdf(new PdfExportOptions { Typefaces = Plain });

            using PdfDocument parsed = PdfDocument.Open(pdf);
            return parsed.GetPage(1).Letters.Select(letter => Math.Round(letter.StartBaseLine.Y, 2)).Distinct().ToList();
        }

        List<double> latin = Baselines("a\nb\nc");
        List<double> mixed = Baselines("a\n" + Salaam + "\nc");
        TypeMetrics own = new OpenTypeMeasurer(Plain.Shaper).GetMetrics(sans);
        TypeMetrics arabic = new OpenTypeMeasurer(Plain.Shaper).GetMetrics(Arabic.WithPointSize(20));

        // The Arabic line's baseline drops by as much further as its face reaches above Noto Sans, and the line after
        // it by as much again as it reaches below.
        Assert.Equal(latin[0] - latin[1] + (arabic.Ascent - own.Ascent), mixed[0] - mixed[1], 2);
        Assert.Equal(latin[1] - latin[2] + (arabic.Descent - own.Descent), mixed[1] - mixed[2], 2);

        // Text in the style's own face alone keeps its own metrics; text set partly in another reaches as far as both.
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(Plain.Shaper);
        TypeMetrics both = measurer.GetMetrics("a " + Salaam, sans);

        Assert.Equal(own, measurer.GetMetrics("abc", sans));
        Assert.Equal((arabic.Ascent, arabic.Descent, own.UnderlineOffset), (both.Ascent, both.Descent, both.UnderlineOffset));
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

    [Theory]
    [InlineData(0x10A00, true)] // Kharoshthi
    [InlineData(0x10D00, true)] // Hanifi Rohingya
    [InlineData(0x11000, true)] // Brahmi
    [InlineData(0x11100, true)] // Chakma
    [InlineData(0x11180, true)] // Sharada
    [InlineData(0x11300, true)] // Grantha
    [InlineData(0x11480, true)] // Tirhuta
    [InlineData(0x11600, true)] // Modi
    [InlineData(0x11680, true)] // Takri
    [InlineData(0x11700, true)] // Ahom
    [InlineData(0x1E900, true)] // Adlam
    [InlineData(0x10000, false)] // Linear B
    [InlineData(0x1D400, false)] // mathematical letters
    [InlineData(0x1F600, false)] // emoji
    public void RunsInScriptsBeyondTheBasicPlaneAreHandedToHarfBuzz(int character, bool complex)
    {
        HarfBuzzShaper shaper = new HarfBuzzShaper();

        Assert.Equal(complex, shaper.Handles($"ab{char.ConvertFromUtf32(character)}".AsSpan()));
    }

    [Fact]
    public void ASurrogateWithoutItsPartnerIsNoComplexCharacter()
    {
        HarfBuzzShaper shaper = new HarfBuzzShaper();

        Assert.False(shaper.Handles("a\uD83A".AsSpan()));
        Assert.False(shaper.Handles("\uD83Aa".AsSpan()));
        Assert.True(shaper.Handles("𞤀".AsSpan()));
    }

    /// <summary>The first and last character of every block whose scripts HarfBuzz shapes.</summary>
    public static TheoryData<int> BlockEdges => new TheoryData<int>
    {
        0x0590, 0x05FF, 0x0600, 0x08FF, 0x0900, 0x0DFF, 0x0E00, 0x0FFF, 0x1000, 0x109F, 0x1780, 0x18AF, 0x1900, 0x1AAF,
        0x1B00, 0x1C4F, 0xA800, 0xA82F, 0xA840, 0xA8FF, 0xA900, 0xAAFF, 0xABC0, 0xABFF, 0xFB1D, 0xFB4F, 0xFB50, 0xFDFF,
        0xFE70, 0xFEFF, 0x10A00, 0x10A5F, 0x10AC0, 0x10AFF, 0x10B80, 0x10BAF, 0x10D00, 0x10D3F, 0x10F30, 0x10FDF,
        0x11000, 0x11AFF, 0x11C00, 0x11DAF, 0x11EE0, 0x11F5F, 0x1E900, 0x1E95F,
    };

    [Theory]
    [MemberData(nameof(BlockEdges))]
    public void EveryBlockIsComplexToItsEdges(int character) =>
        Assert.True(ComplexScriptCharacters.Contains(character), $"U+{character:X4}");

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
    [InlineData(0x109FF)]
    [InlineData(0x10A60)]
    [InlineData(0x10ABF)]
    [InlineData(0x10B00)]
    [InlineData(0x10B7F)]
    [InlineData(0x10BB0)]
    [InlineData(0x10CFF)]
    [InlineData(0x10D40)]
    [InlineData(0x10F2F)]
    [InlineData(0x10FE0)]
    [InlineData(0x10FFF)]
    [InlineData(0x11B00)]
    [InlineData(0x11BFF)]
    [InlineData(0x11DB0)]
    [InlineData(0x11EDF)]
    [InlineData(0x11F60)]
    [InlineData(0x1E8FF)]
    [InlineData(0x1E960)]
    public void CharactersJustOutsideTheBlocksAreNot(int character) =>
        Assert.False(ComplexScriptCharacters.Contains(character), $"U+{character:X4}");

#if NET
    [Fact]
    public void MeasuringShapedTextAllocatesNothingOnceWarm()
    {
        // Every word is measured, and in a right-to-left script each is shaped right to left and put back in logical
        // order; with a feature of the style's own, that is handed to HarfBuzz as well.
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(Shaped.Shaper);
        TypeStyle featured = Arabic.WithFeature("calt", 0);
        float width = measurer.MeasureWidth(Salaam, featured);
        measurer.MeasureWidth(Salaam, featured);

        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int round = 0; round < 10; round++)
            measurer.MeasureWidth(Salaam, featured);

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated == 0, $"{allocated} bytes allocated measuring the text ten times.");
        Assert.Equal(width, measurer.MeasureWidth(Salaam, featured));
    }
#endif
}
