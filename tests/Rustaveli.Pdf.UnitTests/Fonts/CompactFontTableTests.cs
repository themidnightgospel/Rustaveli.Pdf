using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

public class CompactFontTableTests
{
    [Fact]
    public void ReadsTheCommittedCffFont()
    {
        OpenTypeFont font = TestFonts.Cff;
        CompactFontTable cff = font.Cff!;

        Assert.Equal(OutlineFormat.Cff, font.Outlines);
        Assert.Equal("SpecimenCff-Regular", cff.FontName);
        Assert.False(cff.IsCidKeyed);
        Assert.Equal(101, cff.GlyphCount);
        Assert.Equal(font.GlyphCount, cff.GlyphCount);
        Assert.Equal(34, cff.GetCid(34));
        Assert.Null(font.Glyphs);
    }

    [Fact]
    public void MeasuresACffFontFromItsMetricsTables()
    {
        OpenTypeFont font = TestFonts.Cff;

        Assert.Equal(1000, font.UnitsPerEm);
        Assert.Equal(639, font.GetAdvance(font.GetGlyphId('A')));
        Assert.Equal(new LineMetrics(1069, 293, 0, LineMetricsSource.Typographic), font.LineMetrics);
        Assert.Equal("Specimen Cff", font.Names.Family);

        // Without TrueType outlines there is no glyph to measure, so the cap height comes from OS/2 alone.
        Assert.Equal(714, font.Descriptor.CapHeight);
        Assert.False(font.TryGetGlyphBounds(34, out _));
    }

    [Fact]
    public void ReadsANameKeyedSyntheticFont()
    {
        CompactFontTable cff = new CompactFontTable(SyntheticLayout.Cff("Plain-Font", 5));

        Assert.Equal("Plain-Font", cff.FontName);
        Assert.False(cff.IsCidKeyed);
        Assert.Equal(5, cff.GlyphCount);
        Assert.Equal(4, cff.GetCid(4));
    }

    [Fact]
    public void MapsGlyphsToCidsThroughAFormat0Charset()
    {
        byte[] charset = new FontBytes().U8(0).U16(100).U16(7).U16(300).ToArray();
        CompactFontTable cff = new CompactFontTable(SyntheticLayout.Cff("Cid-Font", 4, cidKeyed: true, charset));

        Assert.True(cff.IsCidKeyed);
        Assert.Equal(new[] { 0, 100, 7, 300 }, Enumerable.Range(0, 4).Select(glyph => (int)cff.GetCid((ushort)glyph)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void MapsGlyphsToCidsThroughRangeCharsets(int format)
    {
        FontBytes charset = new FontBytes().U8(format);

        foreach ((int first, int left) in new[] { (1000, 2), (20, 0), (500, 9) })
        {
            charset.U16(first);

            if (format == 1)
                charset.U8(left);
            else
                charset.U16(left);
        }

        CompactFontTable cff =
            new CompactFontTable(SyntheticLayout.Cff("Cid-Font", 6, cidKeyed: true, charset.ToArray()));

        // Three glyphs from 1000, one at 20, then the last range is cut short by the glyph count.
        Assert.Equal(
            new[] { 0, 1000, 1001, 1002, 20, 500 },
            Enumerable.Range(0, 6).Select(glyph => (int)cff.GetCid((ushort)glyph)));
    }

    [Fact]
    public void NumbersGlyphsByIndexForAPredefinedCharset()
    {
        CompactFontTable cff = new CompactFontTable(SyntheticLayout.Cff("Cid-Font", 3, cidKeyed: true));

        Assert.True(cff.IsCidKeyed);
        Assert.Equal(2, cff.GetCid(2));
    }

    [Fact]
    public void RejectsACharsetFormatThatDoesNotExist()
    {
        CompactFontTable cff = new CompactFontTable(SyntheticLayout.Cff("Cid-Font", 3, cidKeyed: true, [7, 0, 0]));

        Assert.Throws<FontFormatException>(() => cff.GetCid(1));
    }

    [Fact]
    public void RejectsAGlyphOutsideTheFont()
    {
        CompactFontTable cff = new CompactFontTable(SyntheticLayout.Cff("Plain-Font", 3));

        Assert.Throws<ArgumentOutOfRangeException>(() => cff.GetCid(3));
    }

    [Fact]
    public void SkipsOperandsOfEveryEncoding()
    {
        // FontBBox as a short int (28), a long int (29), and two-byte positive and negative forms; ItalicAngle as a
        // real number (30), whose nibbles run until a 0xF.
        byte[] entries = new FontBytes()
            .U8(28).I16(-150).U8(29).U32(70000).U8(247).U8(0).U8(251).U8(0).U8(5)
            .U8(30).U8(0x1A).U8(0x25).U8(0xFF).U8(12).U8(2)
            .U8(30).U8(0x5F).U8(12).U8(2)
            .ToArray();

        CompactFontTable cff = new CompactFontTable(SyntheticLayout.Cff("Font", 2, extraDict: entries));

        Assert.Equal(2, cff.GlyphCount);
    }

    [Theory]
    [InlineData(new byte[] { 2, 0, 4, 1 })]
    [InlineData(new byte[] { 1, 0, 4, 1, 0, 0 })]
    [InlineData(new byte[] { 1, 0, 4, 1, 0, 0, 0, 0 })]
    [InlineData(new byte[] { 1, 0, 4, 1, 0, 1, 1, 1, 2, 65, 0, 0 })]
    public void RejectsVersionsAndEmptyFontSets(byte[] data)
    {
        Assert.Throws<FontFormatException>(() => new CompactFontTable(data));
    }

    [Theory]
    [InlineData(255)]
    [InlineData(22)]
    public void RejectsBytesThatBeginNoOperand(int value)
    {
        byte[] table = SyntheticLayout.Cff("Font", 2, extraDict: [(byte)value, 5]);

        Assert.Throws<FontFormatException>(() => new CompactFontTable(table));
    }

    [Fact]
    public void RejectsMoreOperandsThanCffAllows()
    {
        byte[] entries = Enumerable.Repeat((byte)139, 49).Concat(new byte[] { 5 }).ToArray();
        byte[] table = SyntheticLayout.Cff("Font", 2, extraDict: entries);

        Assert.Throws<FontFormatException>(() => new CompactFontTable(table));
    }

    [Fact]
    public void RejectsAFontWithoutCharstrings()
    {
        byte[] table = SyntheticLayout.Cff("Font", 2);

        // The last Top DICT entry is the CharStrings offset: five operand bytes, then operator 17. Make it operator 16.
        int op = Array.LastIndexOf(table, (byte)17, table.Length - 4);
        table[op] = 16;

        Assert.Throws<FontFormatException>(() => new CompactFontTable(table));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void RejectsIndexOffsetSizesThatDoNotExist(int offsetSize)
    {
        byte[] table = SyntheticLayout.Cff("Font", 2);
        table[6] = (byte)offsetSize;

        Assert.Throws<FontFormatException>(() => new CompactFontTable(table));
    }

    [Fact]
    public void RejectsIndexOffsetsOutOfOrder()
    {
        byte[] table = SyntheticLayout.Cff("Font", 2);

        // The Name INDEX's first offset must be 1.
        table[7] = 0;

        Assert.Throws<FontFormatException>(() => new CompactFontTable(table));
    }

    [Fact]
    public void ReadsIndexesWithWiderOffsets()
    {
        CffIndex empty = CffIndex.Read(new byte[] { 0, 0 }, 0);

        foreach (int size in new[] { 2, 3, 4 })
        {
            FontBytes index = new FontBytes().U16(2).U8(size);

            foreach (int offset in new[] { 1, 3, 4 })
            {
                if (size == 2)
                    index.U16(offset);
                else if (size == 3)
                    index.U24(offset);
                else
                    index.U32(offset);
            }

            index.U8(10).U8(11).U8(12);
            CffIndex read = CffIndex.Read(index.ToArray(), 0);

            Assert.Equal(2, read.Count);
            Assert.Equal((3 + (3 * size) + 2, 1), read.GetItem(index.ToArray(), 1));
        }

        Assert.Equal(0, empty.Count);
        Assert.Equal(2, empty.End);
        Assert.Throws<FontFormatException>(() => empty.GetItem(new byte[] { 0, 0 }, 0));
    }

    [Fact]
    public void RejectsAnIndexWhoseDataRunsPastTheTable()
    {
        byte[] index = new FontBytes().U16(1).U8(1).U8(1).U8(200).U8(0).ToArray();

        Assert.Throws<FontFormatException>(() => CffIndex.Read(index, 0));
    }
}
