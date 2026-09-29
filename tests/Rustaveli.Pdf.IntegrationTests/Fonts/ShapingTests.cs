using Rustaveli.Pdf.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Rustaveli.Pdf.IntegrationTests.Fonts;

/// <summary>
/// Text set with the substitutions of the face it is set in, on Noto Sans: glyph ids as fontTools reads them from
/// the committed file, which HarfBuzz shapes the same text to.
/// </summary>
public class ShapingTests
{
    private static readonly TypeStyle Sans = TypeStyle.Default.WithTypeface(TestFonts.Sans).WithPointSize(20);

    private static readonly TypefaceLibrary Library = TestFonts.NewLibrary(includeInstalled: false);

    private static List<ShapedGlyph> Shape(string text, TypeStyle style)
    {
        List<ShapedGlyph> glyphs = [];

        foreach (ShapedGlyph glyph in Library.Shaper.Walk(text.AsSpan(), style))
            glyphs.Add(glyph);

        return glyphs;
    }

    private static Page Export(Action<TextComposer> text)
    {
        byte[] pdf = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(400, 200);
            section.DefaultType = Sans;
            section.Body().Text(text);
        })).ExportPdf(new PdfExportOptions { Typefaces = Library });

        return PdfDocument.Open(pdf).GetPage(1);
    }

    [Fact]
    public void OfficeIsSetWithTheFfiLigature()
    {
        List<ShapedGlyph> glyphs = Shape("office", Sans);

        Assert.Equal(new ushort[] { 82, 1656, 70, 72 }, glyphs.Select(glyph => glyph.Glyph));
        Assert.Equal([(0, 1), (1, 3), (4, 1), (5, 1)], glyphs.Select(glyph => (glyph.Start, glyph.Length)));
        Assert.Equal(["o", "ffi", "c", "e"], glyphs.Select(glyph => glyph.ReadsAs));
    }

    [Fact]
    public void LigaturesCanBeTurnedOff()
    {
        List<ShapedGlyph> glyphs = Shape("office", Sans.Ligatures(false));

        Assert.Equal(new ushort[] { 82, 73, 73, 76, 70, 72 }, glyphs.Select(glyph => glyph.Glyph));
    }

    [Fact]
    public void ALigatureIsMeasuredAsItIsSet()
    {
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(Library.Shaper);
        float ligature = Shape("office", Sans).Sum(glyph => glyph.Advance + glyph.Kerning);

        Assert.Equal(ligature, measurer.MeasureWidth("office", Sans), 0.001f);
    }

    [Fact]
    public void FittingNeverSplitsALigature()
    {
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(Library.Shaper);
        List<ShapedGlyph> glyphs = Shape("office", Sans);

        // Room for the o and part of the ffi: the break falls before the ligature, not inside it.
        float room = glyphs[0].Advance + (glyphs[1].Advance / 2);

        Assert.Equal(1, measurer.MeasureCharactersFitting("office", Sans, room));
    }

    [Fact]
    public void ALigatureReadsBackAsItsCharacters()
    {
        Page page = Export(text => text.Run("office"));

        Assert.Equal("office", page.Text);
        Assert.Contains(page.Letters, letter => letter.Value == "ffi");
    }

    [Fact]
    public void ALetterSetAsTwoGlyphsReadsBackOnce()
    {
        // Noto Sans sets ḿ as an m and a combining acute, both standing for the one character.
        List<ShapedGlyph> glyphs = Shape("ḿ", Sans);
        Page page = Export(text => text.Run("aḿa"));

        Assert.Equal(2, glyphs.Count);
        Assert.Equal(["ḿ", string.Empty], glyphs.Select(glyph => glyph.ReadsAs));
        Assert.Equal("aḿa", page.Text);
    }

    [Theory]
    [InlineData("smcp", "a")]
    [InlineData("onum", "3")]
    [InlineData("pnum", "1")]
    public void AFeatureTurnedOnChangesTheGlyph(string feature, string text)
    {
        ushort plain = Shape(text, Sans).Single().Glyph;
        ushort featured = Shape(text, Sans.WithFeature(feature)).Single().Glyph;

        Assert.NotEqual(plain, featured);
    }

    [Fact]
    public void TheNamedFeaturesMatchTheirTags()
    {
        Assert.Equal(Shape("a", Sans.WithFeature("smcp")).Single().Glyph, Shape("a", Sans.SmallCapitals()).Single().Glyph);
        Assert.Equal(Shape("3", Sans.WithFeature("onum")).Single().Glyph, Shape("3", Sans.OldstyleFigures()).Single().Glyph);
        Assert.Equal(Shape("1", Sans.WithFeature("tnum")).Single().Glyph, Shape("1", Sans.TabularFigures()).Single().Glyph);
        Assert.Equal(Shape("1", Sans.Ligatures(false)).Single().Glyph, Shape("1", Sans.WithFeature("liga", 0)).Single().Glyph);
    }

    [Fact]
    public void KerningCanBeTurnedOff()
    {
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(Library.Shaper);
        TypeStyle unkerned = Sans.WithFeature("kern", 0);
        float advances = Shape("AV", Sans).Sum(glyph => glyph.Advance);

        Assert.NotEqual(0f, Shape("AV", Sans)[1].Kerning);
        Assert.NotEqual(0f, Shape("AV", Sans.WithFeature("kern"))[1].Kerning);
        Assert.Equal(0f, Shape("AV", unkerned)[1].Kerning);
        Assert.Equal(advances, measurer.MeasureWidth("AV", unkerned), 0.001f);
    }

    [Fact]
    public void AFeatureTheFaceLacksChangesNothing()
    {
        Assert.Equal(
            Shape("office", Sans).Select(glyph => glyph.Glyph),
            Shape("office", Sans.WithFeature("zzzz")).Select(glyph => glyph.Glyph));
    }

    [Fact]
    public void SmallCapitalsReadBackAsTheLettersTyped()
    {
        Page page = Export(text => text.Run("small caps").SmallCapitals());

        Assert.Equal("small caps", page.Text);
    }

    private static List<ShapedGlyph> ShapeRightToLeft(string text, TypeStyle style)
    {
        List<ShapedGlyph> glyphs = [];

        foreach (ShapedGlyph glyph in Library.Shaper.Walk(text.AsSpan(), style, rightToLeft: true))
            glyphs.Add(glyph);

        return glyphs;
    }

    [Fact]
    public void RightToLeftTextIsHandedOutLastFirst()
    {
        List<ShapedGlyph> forward = Shape("AVo", Sans);
        List<ShapedGlyph> backward = ShapeRightToLeft("AVo", Sans);

        Assert.Equal(forward.Select(glyph => glyph.Glyph).Reverse(), backward.Select(glyph => glyph.Glyph));
        Assert.Equal([2, 1, 0], backward.Select(glyph => glyph.Start));
    }

    [Fact]
    public void KerningMovesToThePairAsItIsDisplayed()
    {
        // Forward, the kerning between A and V is carried by V; displayed right to left, V comes first and A after it.
        List<ShapedGlyph> forward = Shape("AV", Sans);
        List<ShapedGlyph> backward = ShapeRightToLeft("AV", Sans);

        Assert.NotEqual(0f, forward[1].Kerning);
        Assert.Equal(0f, backward[0].Kerning);
        Assert.Equal(forward[1].Kerning, backward[1].Kerning);
    }

    [Fact]
    public void AMarkStaysAfterTheLetterItSitsOn()
    {
        List<ShapedGlyph> backward = ShapeRightToLeft("aé", Sans.Ligatures(false).WithFeature("ccmp", 0));

        // e and its acute keep their order; the a that came before them is displayed after them.
        Assert.Equal([1, 2, 0], backward.Select(glyph => glyph.Start));
    }

    [Fact]
    public void ALetterSetAsTwoGlyphsKeepsThemInOrderRightToLeft()
    {
        // Noto Sans sets ḿ as an m and a combining acute, the acute standing for no character of its own: displayed
        // right to left, the two move together, before the a that came before them, and in their own order.
        List<ShapedGlyph> letter = Shape("ḿ", Sans);
        ushort a = Shape("a", Sans).Single().Glyph;

        Assert.Equal([letter[0].Glyph, letter[1].Glyph, a], ShapeRightToLeft("aḿ", Sans).Select(glyph => glyph.Glyph));
    }

    [Fact]
    public void ACharacterBeyondTheBasicPlaneIsDisplayedAsACharacterOfItsOwn()
    {
        // U+10301, an Old Italic letter, shares its low sixteen bits with U+0301, a combining acute. Read as the
        // acute, it would be held after the a it follows rather than displayed before it.
        Assert.Equal([1, 0], ShapeRightToLeft("a\U00010301", Sans).Select(glyph => glyph.Start));
    }

    [Theory]
    [InlineData("ab\U0001D167", new[] { 1, 2, 0 })]
    [InlineData("a\U0001F468‍\U0001F469", new[] { 1, 3, 4, 0 })]
    [InlineData("a\U0001F44D\U0001F3FD", new[] { 1, 3, 0 })]
    [InlineData("\U0001F1EC\U0001F1EA\U0001F1FA\U0001F1F8", new[] { 4, 6, 0, 2 })]
    public void WhatReadsAsOneCharacterStaysInOrderRightToLeft(string text, int[] starts)
    {
        // A combining mark beyond the Basic Multilingual Plane stays after its letter; emoji joined by a zero width
        // joiner, an emoji and its skin tone, and the pair of regional indicators making a flag each keep their order.
        Assert.Equal(starts, ShapeRightToLeft(text, Sans).Select(glyph => glyph.Start));
    }

    [Fact]
    public void ASurrogateWithoutItsPartnerIsSetAsACharacterOfItsOwn()
    {
        Assert.Equal([(0xD835, 0, 1), ('a', 1, 1)], Shape("\uD835a", Sans).Select(glyph => (glyph.Codepoint, glyph.Start, glyph.Length)));
        Assert.Equal([('a', 0, 1), (0xD835, 1, 1)], Shape("a\uD835", Sans).Select(glyph => (glyph.Codepoint, glyph.Start, glyph.Length)));
    }

    [Theory]
    [InlineData("©️")]
    [InlineData("©\U000E0100")]
    [InlineData("©᠎")]
    [InlineData("©\u0001")]
    [InlineData("©️\u0001")]
    public void AnInvisibleCharacterTheFaceLacksIsSetAsNothing(string text)
    {
        // Noto Sans has no glyph for a variation selector, a Mongolian vowel separator or a control character, none of
        // which is drawn: each goes with the character before it, rather than being set as a missing-glyph box.
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(Library.Shaper);
        ShapedGlyph copyright = Assert.Single(Shape(text, Sans));

        Assert.Equal(Shape("©", Sans).Single().Glyph, copyright.Glyph);
        Assert.Equal((0, text.Length, text), (copyright.Start, copyright.Length, copyright.ReadsAs));
        Assert.Equal(measurer.MeasureWidth("©", Sans), measurer.MeasureWidth(text, Sans));
        Assert.Empty(measurer.MissingCodepoints);
    }

    [Fact]
    public void AnInvisibleCharacterIsSetAsNothingInAFaceWithoutSubstitutions()
    {
        TypefaceLibrary library = TestFonts.NewLibrary(includeInstalled: false);
        library.RegisterFile(FontAssets.PathOf("SpecimenCff-Regular.otf"));
        TypeStyle specimen = TypeStyle.Default.WithTypeface("Specimen Cff").WithPointSize(20);
        List<ShapedGlyph> glyphs = [];

        foreach (ShapedGlyph glyph in library.Shaper.Walk("a️b\t".AsSpan(), specimen))
            glyphs.Add(glyph);

        Assert.Equal([(0, 2, "a️"), (2, 1, "b"), (3, 1, " ")], glyphs.Select(glyph => (glyph.Start, glyph.Length, glyph.ReadsAs)));
        Assert.DoesNotContain(glyphs, glyph => glyph.Glyph == 0);
    }

    [Fact]
    public void AnInvisibleCharacterWithNothingBeforeItIsLeftOut()
    {
        Assert.Equal([(1, 1, "a")], Shape("️a", Sans).Select(glyph => (glyph.Start, glyph.Length, glyph.ReadsAs)));
        Assert.Empty(Shape("️", Sans));
    }

    [Fact]
    public void ATabIsSetAsASpace()
    {
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(Library.Shaper);
        List<ShapedGlyph> glyphs = Shape("a\tb", Sans);

        Assert.Equal(Shape(" ", Sans).Single().Glyph, glyphs[1].Glyph);
        Assert.Equal(" ", glyphs[1].ReadsAs);
        Assert.Equal(measurer.MeasureWidth("a b", Sans), measurer.MeasureWidth("a\tb", Sans));
    }

    /// <summary>The lines of <paramref name="text"/> set in a column <paramref name="width"/> wide, top first, as read back.</summary>
    private static List<string> Lines(string text, float width)
    {
        byte[] pdf = Document.Compose(composition => composition.Section(section =>
        {
            section.Trim = new Extent(width, 200);
            section.DefaultType = Sans;
            section.Body().Text(text);
        })).ExportPdf(new PdfExportOptions { Typefaces = Library });

        using PdfDocument parsed = PdfDocument.Open(pdf);

        // A soft hyphen that is not shown still reads back, with the letter before it; the lines are compared as seen.
        return parsed.GetPage(1).Letters
            .GroupBy(letter => Math.Round(letter.StartBaseLine.Y))
            .OrderByDescending(line => line.Key)
            .Select(line => string.Concat(line.Select(letter => letter.Value)).Replace("­", string.Empty))
            .ToList();
    }

    [Fact]
    public void ASoftHyphenIsShownOnlyWhereTheLineBreaks()
    {
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(Library.Shaper);
        const string Word = "hy­phen­ation";

        Assert.Equal(measurer.MeasureWidth("hyphenation", Sans), measurer.MeasureWidth(Word, Sans));
        Assert.Equal(0f, measurer.MeasureWidth("­", Sans));

        Assert.Equal(["hyphenation"], Lines(Word, measurer.MeasureWidth("hyphenation", Sans) + 1));
        Assert.Equal(["hyphen-", "ation"], Lines(Word, measurer.MeasureWidth("hyphen-", Sans) + 1));
    }

    [Fact]
    public void ASoftHyphenEndingTheTextIsNotShown()
    {
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(Library.Shaper);

        Assert.Equal(["hyphen"], Lines("hyphen­", measurer.MeasureWidth("hyphen", Sans) + 1));
        Assert.Equal(["hyphen", "hyphen"], Lines("hyphen­\nhyphen", measurer.MeasureWidth("hyphen", Sans) + 1));
    }

    [Fact]
    public void BracketsReadingRightToLeftAreMirrored()
    {
        ushort opening = Shape("(", Sans).Single().Glyph;
        ushort closing = Shape(")", Sans).Single().Glyph;

        // The closing bracket, read last, is displayed first — mirrored into an opening one — and the opening last.
        Assert.Equal([opening, closing], ShapeRightToLeft("(x)", Sans).Where(glyph => glyph.Codepoint != 'x').Select(glyph => glyph.Glyph));
        Assert.Equal(closing, ShapeRightToLeft("(", Sans).Single().Glyph);
    }

    [Fact]
    public void RightToLeftTextReadsBackInLogicalOrder()
    {
        Page page = Export(text => text.Run("abc ").RightToLeft());

        Assert.Contains("abc", page.Text);
    }

    [Fact]
    public void AWalkLeftEarlyLeavesTheNextWalkItsBuffer()
    {
        foreach (ShapedGlyph glyph in Library.Shaper.Walk("office".AsSpan(), Sans))
            break;

        Assert.Equal(4, Shape("office", Sans).Count);
    }

#if NET
    [Fact]
    public void MeasuringTextSetWithSubstitutionsAllocatesNothingOnceWarm()
    {
        // Every word of a document is measured, through the face's ligature and contextual lookups; once the lookups
        // are known, measuring again must cost nothing. (A ligature that forms carries the characters it stands for
        // as a string of its own, which the text read back from a PDF needs; none forms here.)
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(Library.Shaper);
        const string Text = "The quick fjord, 1/2 and 3/4.";
        float width = measurer.MeasureWidth(Text, Sans);
        measurer.MeasureWidth(Text, Sans);
        float[] again = new float[10];

        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int round = 0; round < again.Length; round++)
            again[round] = measurer.MeasureWidth(Text, Sans);

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated == 0, $"{allocated} bytes allocated measuring the text ten times.");
        Assert.All(again, measured => Assert.Equal(width, measured));
    }

    [Fact]
    public void MeasuringALigatureAllocatesNothingOnceWarm()
    {
        // The characters a ligature stands for are what the text read back from a PDF needs, not what measuring does.
        OpenTypeMeasurer measurer = new OpenTypeMeasurer(Library.Shaper);
        const string Text = "An office affair.";
        float width = measurer.MeasureWidth(Text, Sans);
        measurer.MeasureCharactersFitting(Text, Sans, width / 2);

        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int round = 0; round < 10; round++)
        {
            measurer.MeasureWidth(Text, Sans);
            measurer.MeasureCharactersFitting(Text, Sans, width / 2);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated == 0, $"{allocated} bytes allocated measuring the text ten times.");
        Assert.Contains(Shape(Text, Sans), glyph => glyph.ReadsAs == "ffi");
    }
#endif
}
