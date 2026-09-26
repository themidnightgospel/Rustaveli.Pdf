using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

public class CharacterMapTests
{
    private const int GlyphCount = 400;

    private static CharacterMap Map(params (int Platform, int Encoding, byte[] Subtable)[] subtables) =>
        new CharacterMap(SyntheticTables.Cmap(subtables), GlyphCount);

    [Fact]
    public void ReadsFormat0()
    {
        CharacterMap map = Map((1, 0, SyntheticTables.Format0(('A', 5), (0x8E, 6))));

        Assert.Equal(CharacterEncoding.MacRoman, map.Encoding);
        Assert.Equal(5, map.GetGlyph('A'));

        // Mac Roman byte 0x8E is é.
        Assert.Equal(6, map.GetGlyph('é'));
        Assert.Equal(0, map.GetGlyph('一'));
        Assert.Equal(0, map.GetGlyph(0x1F600));
        Assert.Equal(
            new[] { ('A', 5), ('é', 6) },
            map.EnumerateMappings().Select(pair => ((char)pair.Key, (int)pair.Value)));
    }

    [Fact]
    public void ReadsFormat4DeltaAndGlyphArraySegments()
    {
        CharacterMap map = Map((3, 1, SyntheticTables.Format4Segments(
            ('A', 'C', 10 - 'A', null),
            ('a', 'c', 0, [20, 0, 22]),
            ('x', 'x', 5, [30]))));

        Assert.Equal(CharacterEncoding.Unicode, map.Encoding);
        Assert.Equal(10, map.GetGlyph('A'));
        Assert.Equal(12, map.GetGlyph('C'));
        Assert.Equal(20, map.GetGlyph('a'));

        // A zero in the glyph array is a gap, even when the segment adds a delta.
        Assert.Equal(0, map.GetGlyph('b'));
        Assert.Equal(22, map.GetGlyph('c'));
        Assert.Equal(35, map.GetGlyph('x'));
        Assert.Equal(0, map.GetGlyph('D'));
        Assert.Equal(0, map.GetGlyph('ÿ'));
        Assert.Equal(0, map.GetGlyph(0xFFFF));
        Assert.Equal(0, map.GetGlyph(0x10000));
        Assert.Equal(0, map.GetGlyph(-1));
        Assert.Equal(
            new[] { ('A', 10), ('B', 11), ('C', 12), ('a', 20), ('c', 22), ('x', 35) },
            map.EnumerateMappings().Select(pair => ((char)pair.Key, (int)pair.Value)));
    }

    [Fact]
    public void LooksUpPastTheLatinCacheInFormat4()
    {
        CharacterMap map = Map((3, 1, SyntheticTables.Format4(('Ж', 7), ('ა', 8))));

        Assert.Equal(7, map.GetGlyph('Ж'));
        Assert.Equal(8, map.GetGlyph('ა'));
        Assert.Equal(0, map.GetGlyph('ბ'));
    }

    [Fact]
    public void TreatsAGlyphArrayOffsetPastTheTableAsNoGlyph()
    {
        byte[] subtable = SyntheticTables.Format4Segments(('a', 'a', 0, [20]));

        // Point the first segment's range offset far beyond the table.
        int segments = BigEndian.UInt16(subtable, 6) / 2;
        int rangeOffsets = 14 + (segments * 2) + 2 + (segments * 4);
        BigEndian.WriteUInt16(subtable, rangeOffsets, 0x7FFE);

        Assert.Equal(0, Map((3, 1, subtable)).GetGlyph('a'));
    }

    [Fact]
    public void ReadsFormat6()
    {
        CharacterMap map = Map((3, 1, SyntheticTables.Format6(0x0410, 40, 0, 42)));

        Assert.Equal(40, map.GetGlyph(0x0410));
        Assert.Equal(0, map.GetGlyph(0x0411));
        Assert.Equal(42, map.GetGlyph(0x0412));
        Assert.Equal(0, map.GetGlyph(0x0413));
        Assert.Equal(0, map.GetGlyph(0x040F));
        Assert.Equal(new[] { 0x0410, 0x0412 }, map.EnumerateMappings().Select(pair => pair.Key));
    }

    [Fact]
    public void ReadsFormat12AcrossPlanes()
    {
        CharacterMap map = Map((3, 10, SyntheticTables.Format12(false, ('A', 'C', 10), (0x1F600, 0x1F602, 300))));

        Assert.Equal(11, map.GetGlyph('B'));
        Assert.Equal(301, map.GetGlyph(0x1F601));
        Assert.Equal(0, map.GetGlyph(0x1F603));
        Assert.Equal(0, map.GetGlyph('@'));
        Assert.Equal(0, map.GetGlyph('D'));
        Assert.Equal(0, map.GetGlyph(-5));
        Assert.Equal(6, map.EnumerateMappings().Count());
    }

    [Fact]
    public void ReadsFormat13AsOneGlyphPerRange()
    {
        CharacterMap map = Map((3, 10, SyntheticTables.Format12(true, (0x4E00, 0x9FFF, 7))));

        Assert.Equal(7, map.GetGlyph(0x4E00));
        Assert.Equal(7, map.GetGlyph(0x6C34));
        Assert.Equal(0x9FFF - 0x4E00 + 1, map.EnumerateMappings().Count());
    }

    [Fact]
    public void TreatsGlyphsBeyondTheFontOrSixteenBitsAsMissing()
    {
        CharacterMap map = Map((3, 10, SyntheticTables.Format12(false, ('A', 'A', GlyphCount), ('B', 'B', 0x10000))));

        Assert.Equal(0, map.GetGlyph('A'));
        Assert.Equal(0, map.GetGlyph('B'));
        Assert.Empty(map.EnumerateMappings());
    }

    [Fact]
    public void EnumeratesOverlappingGroupsOnce()
    {
        // Malformed: the second group overlaps the first and the third runs past Unicode. Each code still comes once.
        CharacterMap map = Map((3, 10, SyntheticTables.Format12(
            false, ('A', 'Z', 1), ('M', 'P', 100), (0x10FFFE, 0xFFFFFFFF, 200))));

        int[] codes = map.EnumerateMappings().Select(pair => pair.Key).ToArray();

        Assert.Equal(codes.Distinct().Count(), codes.Length);
        Assert.Equal(28, codes.Length);
        Assert.Equal(0x10FFFF, codes[codes.Length - 1]);
    }

    [Fact]
    public void EnumeratesOverlappingFormat4SegmentsOnce()
    {
        CharacterMap map = Map((3, 1, SyntheticTables.Format4Segments(('A', 'D', 0, null), ('C', 'F', 0, null))));

        Assert.Equal(new[] { 'A', 'B', 'C', 'D', 'E', 'F' }, map.EnumerateMappings().Select(pair => (char)pair.Key));
    }

    [Fact]
    public void PrefersTheFullRepertoireSubtable()
    {
        CharacterMap map = Map(
            (1, 0, SyntheticTables.Format0(('A', 1))),
            (3, 1, SyntheticTables.Format4(('A', 2))),
            (3, 10, SyntheticTables.Format12(false, ('A', 'A', 3))),
            (0, 3, SyntheticTables.Format4(('A', 4))));

        Assert.Equal(3, map.GetGlyph('A'));
    }

    [Theory]
    [InlineData(3, 10, 0, 4)]
    [InlineData(0, 6, 3, 1)]
    [InlineData(3, 1, 0, 3)]
    [InlineData(0, 3, 0, 0)]
    [InlineData(0, 2, 3, 0)]
    [InlineData(3, 0, 1, 0)]
    public void RanksSubtablesByPlatformAndEncoding(int platform, int encoding, int lesserPlatform, int lesserEncoding)
    {
        byte[] preferred = SyntheticTables.Format4((0xF041, 1), ('A', 1));
        byte[] lesser = SyntheticTables.Format4((0xF041, 2), ('A', 2));

        // Whichever order the subtables are listed in.
        Assert.Equal(1, Map((lesserPlatform, lesserEncoding, lesser), (platform, encoding, preferred)).GetGlyph('A'));
        Assert.Equal(1, Map((platform, encoding, preferred), (lesserPlatform, lesserEncoding, lesser)).GetGlyph('A'));
    }

    [Fact]
    public void SkipsSubtablesInFormatsItDoesNotRead()
    {
        byte[] format2 = new FontBytes().U16(2).U16(6).U16(0).ToArray();
        CharacterMap map = Map((3, 10, format2), (3, 1, SyntheticTables.Format4(('A', 9))));

        Assert.Equal(9, map.GetGlyph('A'));
    }

    [Fact]
    public void FindsSymbolFontCharactersInThePrivateUseArea()
    {
        CharacterMap map = Map((3, 0, SyntheticTables.Format4((0xF041, 8), (0x263A, 9))));

        Assert.Equal(CharacterEncoding.Symbol, map.Encoding);
        Assert.Equal(8, map.GetGlyph('A'));
        Assert.Equal(8, map.GetGlyph(0xF041));
        Assert.Equal(9, map.GetGlyph(0x263A));
        Assert.Equal(0, map.GetGlyph('B'));
        Assert.Equal(0, map.GetGlyph(0x263B));
    }

    [Fact]
    public void MapsNothingWithoutAUsableSubtable()
    {
        CharacterMap map = Map((2, 0, SyntheticTables.Format4(('A', 1))), (0, 5, SyntheticTables.Format4(('A', 1))));

        Assert.Equal(CharacterEncoding.None, map.Encoding);
        Assert.Equal(0, map.GetGlyph('A'));
        Assert.Equal(0, map.GetGlyph(0x4E00));
        Assert.Empty(map.EnumerateMappings());
    }

    [Fact]
    public void RejectsASubtableOffsetPastTheTable()
    {
        byte[] table = new FontBytes().U16(0).U16(1).U16(3).U16(1).U32(5000).ToArray();

        Assert.Throws<FontFormatException>(() => new CharacterMap(table, GlyphCount));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(12)]
    public void RejectsTruncatedSubtables(int format)
    {
        byte[] subtable = format switch
        {
            0 => SyntheticTables.Format0(('A', 1)),
            4 => SyntheticTables.Format4(('A', 1)),
            6 => SyntheticTables.Format6('A', 1, 2, 3),
            _ => SyntheticTables.Format12(false, ('A', 'A', 1))
        };

        byte[] truncated = subtable.Take(subtable.Length - 3).ToArray();

        Assert.Throws<FontFormatException>(() => Map((3, 1, truncated)));
    }

    [Fact]
    public void RejectsAGroupCountNoTableCouldHold()
    {
        byte[] subtable = new FontBytes().U16(12).U16(0).U32(28).U32(0).U32(0x7FFFFFFF).ToArray();

        Assert.Throws<FontFormatException>(() => Map((3, 10, subtable)));
    }

    [Fact]
    public void MapsTheCommittedFontsLikeFontTools()
    {
        // Spot checks against fontTools' getBestCmap on the same files.
        Assert.Equal(CharacterEncoding.Unicode, TestFonts.Regular.CharacterMap.Encoding);
        Assert.Equal(111, TestFonts.Georgian.GetGlyphId(0x10D0));
        Assert.Equal(98, TestFonts.SpecimenRegular.GetGlyphId(0xC5));
        Assert.Equal(101, TestFonts.SpecimenRegular.GetGlyphId(0xE000));
        Assert.Equal(104, TestFonts.SpecimenRegular.GetGlyphId(0xE003));
        Assert.Equal(34, TestFonts.Cff.GetGlyphId('A'));
    }
}
