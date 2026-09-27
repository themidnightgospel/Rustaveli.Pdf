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

    [Fact]
    public void AWalkLeftEarlyLeavesTheNextWalkItsBuffer()
    {
        foreach (ShapedGlyph glyph in Library.Shaper.Walk("office".AsSpan(), Sans))
            break;

        Assert.Equal(4, Shape("office", Sans).Count);
    }
}
