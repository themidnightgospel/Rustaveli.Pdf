using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>
/// CFF fonts cut down to the glyphs shown: a CID-keyed CFF whose CIDs are the codes shown, whose glyphs call no
/// subroutine, and which keeps every font dict — read back by the same reader any CFF font is.
/// </summary>
public class CffSubsetterTests
{
    private const int FontMatrix = (12 << 8) | 7;

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

        byte[] subset = CffSubsetter.TrySubset(CffOf(font), font.UnitsPerEm,shown)!;
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

        byte[] subset = CffSubsetter.TrySubset(CffOf(font), font.UnitsPerEm,shown)!;
        CompactFontTable read = new CompactFontTable(subset);

        Assert.True(read.IsCidKeyed);
        Assert.Equal([0, .. shown.Select(glyph => glyph.Cid).Distinct().Order()], Enumerable.Range(0, read.GlyphCount).Select(glyph => read.GetCid((ushort)glyph)));
        Assert.Equal(FontDictCount(CffOf(font).ToArray()), FontDictCount(subset));
        Assert.True(subset.Length < CffOf(font).Length / 5);
        AssertNoSubroutines(subset);
    }

    [Fact]
    public void AFontDictKeepsOnlyItsFontMatrixAndPrivateDict()
    {
        // The source's font dicts name themselves, by strings the subset does not carry.
        byte[] source = CffOf(Cjk).ToArray();
        Assert.Contains((12 << 8) | 38, FontDictOperators(source));

        byte[] subset = CffSubsetter.TrySubset(CffOf(Cjk), 1000,Shown(Cjk, "中あA"))!;

        Assert.Subset(new HashSet<int> { (12 << 8) | 7, 18 }, FontDictOperators(subset));
        Assert.Contains(18, FontDictOperators(subset));
    }

    [Fact]
    public void ANameKeyedFontsOneFontDictHoldsOnlyItsPrivateDict()
    {
        byte[] subset = CffSubsetter.TrySubset(CffOf(Subrs), 1000,Shown(Subrs, "Ab"))!;

        Assert.Equal([18], FontDictOperators(subset));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(3000)]
    [InlineData(16384)]
    public void AFontScaledByItsUnitsPerEmAloneIsGivenTheMatrixThatScalesIt(int unitsPerEm)
    {
        // Inside an OpenType font a CFF table with no font matrix is scaled by the head table's units per em; standing
        // alone, by a thousand, unless it says otherwise.
        byte[] subset = CffSubsetter.TrySubset(CffOf(Subrs), unitsPerEm, Shown(Subrs, "Ab"))!;

        double scale = 1d / unitsPerEm;
        Assert.Equal([scale, 0, 0, scale, 0, 0], TopDict(subset).Single(entry => entry.Operator == FontMatrix).Operands);
        AssertNoSubroutines(subset);
    }

    [Fact]
    public void AFontScaledByAThousandOrByItsOwnMatrixIsGivenNoOther()
    {
        byte[] thousand = CffSubsetter.TrySubset(CffOf(Subrs), 1000, Shown(Subrs, "Ab"))!;
        Assert.DoesNotContain(TopDict(thousand), entry => entry.Operator == FontMatrix);

        // A subset scaled by 2048 has a matrix of its own, which a subset of it keeps as it is.
        byte[] scaled = CffSubsetter.TrySubset(CffOf(Subrs), 2048, Shown(Subrs, "Ab"))!;
        CompactFontTable read = new CompactFontTable(scaled);
        byte[] again = CffSubsetter.TrySubset(scaled, 2048, [(read.GetCid(1), 1)])!;

        CffDictEntry matrix = Assert.Single(TopDict(again), entry => entry.Operator == FontMatrix);
        Assert.Equal(1d / 2048, matrix.Operands[0]);
    }

    [Fact]
    public void AFontWhoseFontDictsScaleItIsNotScaledAgainByTheTopDict()
    {
        // A reader concatenates the top dict's matrix with a font dict's, so a top matrix scaling by 1/2048 over a
        // font dict already scaling by 1/2048 would draw every glyph 2048 times too small. The font dict that has no
        // matrix of its own is given the one that scales by the units per em instead.
        double scale = 1d / 2048;
        byte[] source = CidKeyedCff([scale, 0, 0, scale, 0, 0], null);

        byte[] subset = CffSubsetter.TrySubset(source, 2048, [(1, 1), (2, 2)])!;

        Assert.DoesNotContain(TopDict(subset), entry => entry.Operator == FontMatrix);
        Assert.All(
            FontDicts(subset),
            dict => Assert.Equal([scale, 0, 0, scale, 0, 0], dict.Single(entry => entry.Operator == FontMatrix).Operands));
    }

    [Fact]
    public void ACidKeyedFontWhoseFontDictsHaveNoMatrixIsScaledByTheTopDict()
    {
        byte[] subset = CffSubsetter.TrySubset(CffOf(Cjk), 2048, Shown(Cjk, "中あA"))!;

        double scale = 1d / 2048;
        Assert.Equal([scale, 0, 0, scale, 0, 0], TopDict(subset).Single(entry => entry.Operator == FontMatrix).Operands);
        Assert.DoesNotContain(FontDictOperators(subset), op => op == FontMatrix);
    }

    [Fact]
    public void AGlyphShownTwiceIsKeptOnceAndNotdefIsAlwaysFirst()
    {
        OpenTypeFont font = Subrs;
        ushort a = font.GetGlyphId('A');

        CompactFontTable read = new CompactFontTable(CffSubsetter.TrySubset(CffOf(font), font.UnitsPerEm,[(a, a), (a, a), (0, 0)])!);

        Assert.Equal(2, read.GlyphCount);
        Assert.Equal([0, a], new[] { read.GetCid(0), read.GetCid(1) });
    }

    [Fact]
    public void AFontThatCannotBeSubsetIsLeftWhole()
    {
        byte[] cff = CffOf(Subrs).ToArray();

        byte[] version2 = [.. cff];
        version2[0] = 2;

        Assert.Null(CffSubsetter.TrySubset(version2, 1000, []));
        Assert.Null(CffSubsetter.TrySubset(cff.AsMemory(0, 3), 1000, []));
        Assert.Null(CffSubsetter.TrySubset(cff.AsMemory(0, 40), 1000, []));
        Assert.Null(CffSubsetter.TrySubset(CffOf(Subrs), 1000,[(5000, 5000)]));
    }

    private static List<CffDictEntry> TopDict(byte[] cff)
    {
        CffIndex names = CffIndex.Read(cff, cff[2]);
        CffIndex tops = CffIndex.Read(cff, names.End);
        (int start, int length) = tops.GetItem(cff, 0);
        return CffDict.Read(cff.AsSpan(start, length));
    }

    /// <summary>
    /// A CID-keyed CFF of three empty glyphs with no top dict font matrix and one font dict per matrix given — null
    /// for one without — glyph <c>n</c> using font dict <c>n</c> modulo their number. Private dicts are empty.
    /// </summary>
    private static byte[] CidKeyedCff(params double[]?[] matrices)
    {
        const int GlyphCount = 3;
        byte[] name = [(byte)'C', (byte)'i', (byte)'d'];

        // Every offset is written in five bytes, so the top dict's size does not depend on the values in it.
        byte[] Top(int fdSelect, int charStrings, int fdArray) => new FontBytes()
            .U8(139).U8(139).U8(139).U8(12).U8(30)
            .U8(29).U32(fdSelect).U8(12).U8(37)
            .U8(29).U32(charStrings).U8(17)
            .U8(29).U32(fdArray).U8(12).U8(36)
            .ToArray();

        byte[][] fontDicts = matrices.Select(matrix =>
        {
            FontBytes dict = new FontBytes();

            if (matrix is not null)
            {
                foreach (double value in matrix)
                    dict.Bytes(Real(value));

                dict.U8(12).U8(7);
            }

            return dict.U8(139).U8(139).U8(18).ToArray();
        }).ToArray();

        int fdSelectAt = 4 + IndexLength([name]) + IndexLength([Top(0, 0, 0)]) + 2 + 2;
        int charStringsAt = fdSelectAt + 1 + GlyphCount;
        byte[][] glyphs = Enumerable.Repeat(new byte[] { 14 }, GlyphCount).ToArray();
        int fdArrayAt = charStringsAt + IndexLength(glyphs);

        FontBytes cff = new FontBytes().U8(1).U8(0).U8(4).U8(4);
        Index(cff, [name]);
        Index(cff, [Top(fdSelectAt, charStringsAt, fdArrayAt)]);
        cff.U16(0).U16(0).U8(0);

        for (int glyph = 0; glyph < GlyphCount; glyph++)
            cff.U8(glyph % matrices.Length);

        Index(cff, glyphs);
        Index(cff, fontDicts);
        return cff.ToArray();
    }

    /// <summary>A real number in a DICT, written in the nibbles of its decimal digits and point.</summary>
    private static byte[] Real(double value)
    {
        string text = value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        List<int> nibbles = [.. text.Select(character => character == '.' ? 0xA : character - '0'), 0xF];

        if (nibbles.Count % 2 == 1)
            nibbles.Add(0xF);

        FontBytes real = new FontBytes().U8(30);

        for (int index = 0; index < nibbles.Count; index += 2)
            real.U8((nibbles[index] << 4) | nibbles[index + 1]);

        return real.ToArray();
    }

    /// <summary>An INDEX with one-byte offsets, which is all these small tables need.</summary>
    private static void Index(FontBytes table, byte[][] items)
    {
        table.U16(items.Length).U8(1).U8(1);
        int offset = 1;

        foreach (byte[] item in items)
        {
            offset += item.Length;
            table.U8(offset);
        }

        foreach (byte[] item in items)
            table.Bytes(item);
    }

    private static int IndexLength(byte[][] items) => 3 + items.Length + 1 + items.Sum(item => item.Length);

    /// <summary>The entries of each of the font dicts of <paramref name="cff"/>.</summary>
    private static List<List<CffDictEntry>> FontDicts(byte[] cff)
    {
        CffIndex fdArray = CffIndex.Read(cff, TopDict(cff).Single(entry => entry.Operator == ((12 << 8) | 36)).Integer());
        List<List<CffDictEntry>> dicts = [];

        for (int index = 0; index < fdArray.Count; index++)
        {
            (int start, int length) = fdArray.GetItem(cff, index);
            dicts.Add(CffDict.Read(cff.AsSpan(start, length)));
        }

        return dicts;
    }

    private static int FontDictCount(byte[] cff)
    {
        CffIndex names = CffIndex.Read(cff, cff[2]);
        CffIndex tops = CffIndex.Read(cff, names.End);
        (int start, int length) = tops.GetItem(cff, 0);
        CffDictEntry fdArray = CffDict.Read(cff.AsSpan(start, length)).Single(entry => entry.Operator == ((12 << 8) | 36));
        return CffIndex.Read(cff, fdArray.Integer()).Count;
    }

    /// <summary>Every operator any font dict of <paramref name="cff"/> has, once each.</summary>
    private static HashSet<int> FontDictOperators(byte[] cff)
    {
        CffIndex names = CffIndex.Read(cff, cff[2]);
        CffIndex tops = CffIndex.Read(cff, names.End);
        (int start, int length) = tops.GetItem(cff, 0);
        CffIndex fdArray = CffIndex.Read(cff, CffDict.Read(cff.AsSpan(start, length)).Single(entry => entry.Operator == ((12 << 8) | 36)).Integer());
        HashSet<int> operators = [];

        for (int index = 0; index < fdArray.Count; index++)
        {
            (int fdStart, int fdLength) = fdArray.GetItem(cff, index);
            operators.UnionWith(CffDict.Read(cff.AsSpan(fdStart, fdLength)).Select(entry => entry.Operator));
        }

        return operators;
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
