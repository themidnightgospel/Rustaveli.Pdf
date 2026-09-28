using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>
/// CFF fonts cut down to the glyphs shown: a CID-keyed CFF whose CIDs are the codes shown, whose glyphs call no
/// subroutine, and which keeps every font dict — read back by the same reader any CFF font is.
/// </summary>
public class CffSubsetterTests
{
    private static OpenTypeFont Subrs => TestFonts.Load("SpecimenSubrs-Regular.otf");

    private static OpenTypeFont Cjk => TestFonts.Load("SpecimenCjk-Regular.otf");

    private static ReadOnlyMemory<byte> CffOf(OpenTypeFont font)
    {
        Assert.True(font.TryGetTable(TableTag.Cff, out ReadOnlyMemory<byte> table));
        return table;
    }

    /// <summary>The codes a document shows <paramref name="text"/> by: CIDs in a CID-keyed font, glyph indices otherwise.</summary>
    private static List<(ushort Cid, ushort Glyph)> Shown(OpenTypeFont font, string text) =>
        text.Select(character => font.GetGlyphId(character))
            .Select(glyph => (font.Cff!.GetCid(glyph), glyph))
            .ToList();

    [Fact]
    public void ANameKeyedFontBecomesACidKeyedSubsetWhoseCidsAreItsGlyphIndices()
    {
        OpenTypeFont font = Subrs;
        List<(ushort Cid, ushort Glyph)> shown = Shown(font, "Hello, Åé");

        byte[] subset = CffSubsetter.TrySubset(CffOf(font), shown)!;
        CompactFontTable read = new CompactFontTable(subset);

        Assert.True(read.IsCidKeyed);
        Assert.Equal(font.Cff!.FontName, read.FontName);
        Assert.Equal(shown.Distinct().Count() + 1, read.GlyphCount);
        Assert.Equal([0, .. shown.Select(glyph => glyph.Cid).Distinct().Order()], Enumerable.Range(0, read.GlyphCount).Select(glyph => read.GetCid((ushort)glyph)));
        Assert.True(subset.Length < CffOf(font).Length / 3);
        AssertNoSubroutines(subset);
    }

    [Fact]
    public void ACidKeyedFontKeepsItsCidsAndEveryFontDict()
    {
        OpenTypeFont font = Cjk;
        List<(ushort Cid, ushort Glyph)> shown = Shown(font, "中国人永鬱龘あアHello");

        byte[] subset = CffSubsetter.TrySubset(CffOf(font), shown)!;
        CompactFontTable read = new CompactFontTable(subset);

        Assert.True(read.IsCidKeyed);
        Assert.Equal([0, .. shown.Select(glyph => glyph.Cid).Distinct().Order()], Enumerable.Range(0, read.GlyphCount).Select(glyph => read.GetCid((ushort)glyph)));
        Assert.Equal(FontDictCount(CffOf(font).ToArray()), FontDictCount(subset));
        Assert.True(subset.Length < CffOf(font).Length / 5);
        AssertNoSubroutines(subset);
    }

    [Fact]
    public void AGlyphShownTwiceIsKeptOnceAndNotdefIsAlwaysFirst()
    {
        OpenTypeFont font = Subrs;
        ushort a = font.GetGlyphId('A');

        CompactFontTable read = new CompactFontTable(CffSubsetter.TrySubset(CffOf(font), [(a, a), (a, a), (0, 0)])!);

        Assert.Equal(2, read.GlyphCount);
        Assert.Equal([0, a], new[] { read.GetCid(0), read.GetCid(1) });
    }

    [Fact]
    public void AFontThatCannotBeSubsetIsLeftWhole()
    {
        byte[] cff = CffOf(Subrs).ToArray();

        byte[] version2 = [.. cff];
        version2[0] = 2;

        Assert.Null(CffSubsetter.TrySubset(version2, []));
        Assert.Null(CffSubsetter.TrySubset(cff.AsMemory(0, 40), []));
        Assert.Null(CffSubsetter.TrySubset(CffOf(Subrs), [(5000, 5000)]));
    }

    private static int FontDictCount(byte[] cff)
    {
        CffIndex names = CffIndex.Read(cff, cff[2]);
        CffIndex tops = CffIndex.Read(cff, names.End);
        (int start, int length) = tops.GetItem(cff, 0);
        CffDictEntry fdArray = CffDict.Read(cff.AsSpan(start, length)).Single(entry => entry.Operator == ((12 << 8) | 36));
        return CffIndex.Read(cff, fdArray.Integer()).Count;
    }

    /// <summary>No global subroutines, no private dict naming local ones, and no glyph calling either.</summary>
    private static void AssertNoSubroutines(byte[] cff)
    {
        CffIndex names = CffIndex.Read(cff, cff[2]);
        CffIndex tops = CffIndex.Read(cff, names.End);
        CffIndex strings = CffIndex.Read(cff, tops.End);
        Assert.Equal(0, CffIndex.Read(cff, strings.End).Count);

        (int start, int length) = tops.GetItem(cff, 0);
        List<CffDictEntry> top = CffDict.Read(cff.AsSpan(start, length));
        CffIndex fdArray = CffIndex.Read(cff, top.Single(entry => entry.Operator == ((12 << 8) | 36)).Integer());

        for (int index = 0; index < fdArray.Count; index++)
        {
            (int fdStart, int fdLength) = fdArray.GetItem(cff, index);
            CffDictEntry privateEntry = CffDict.Read(cff.AsSpan(fdStart, fdLength)).Single(entry => entry.Operator == 18);
            Assert.DoesNotContain(CffDict.Read(cff.AsSpan(privateEntry.Integer(1), privateEntry.Integer(0))), entry => entry.Operator == 19);
        }

        // Flattening again changes nothing: there is nothing left to write out.
        CffIndex charStrings = CffIndex.Read(cff, top.Single(entry => entry.Operator == 17).Integer());

        for (int glyph = 0; glyph < charStrings.Count; glyph++)
        {
            (int glyphStart, int glyphLength) = charStrings.GetItem(cff, glyph);
            Assert.Equal(cff.AsSpan(glyphStart, glyphLength).ToArray(), CharStringFlattener.Flatten(cff, glyphStart, glyphLength, default, default));
        }
    }
}
