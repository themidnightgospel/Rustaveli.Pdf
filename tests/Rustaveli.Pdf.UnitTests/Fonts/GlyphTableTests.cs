using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

public class GlyphTableTests
{
    private static readonly byte[] Outline = SyntheticTables.SimpleGlyph(10, -20, 300, 400);

    private static GlyphTable Table(bool longOffsets, params byte[][] glyphs)
    {
        (byte[] glyf, byte[] loca) = SyntheticTables.GlyphData(longOffsets, glyphs);
        return new GlyphTable(glyf, loca, glyphs.Length, longOffsets ? (short)1 : (short)0);
    }

    /// <summary>
    /// A <c>glyf</c> table of <paramref name="length"/> bytes, its glyphs where long offsets put them.
    /// </summary>
    private static GlyphTable Located(int length, params uint[] offsets)
    {
        FontBytes loca = new FontBytes();

        foreach (uint offset in offsets)
            loca.U32(offset);

        return new GlyphTable(new byte[length], loca.ToArray(), offsets.Length - 1, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LocatesGlyphsThroughEitherLocaFormat(bool longOffsets)
    {
        GlyphTable table = Table(longOffsets, Outline, [], Outline);

        Assert.Equal(3, table.GlyphCount);
        Assert.Equal(Outline, table.GetGlyphData(0).ToArray().Take(Outline.Length));
        Assert.True(table.GetGlyphData(1).IsEmpty);
        Assert.True(table.TryGetBounds(2, out GlyphBounds bounds));
        Assert.Equal(new GlyphBounds(10, -20, 300, 400), bounds);
        Assert.False(table.TryGetBounds(1, out _));
    }

    [Fact]
    public void ReadsCompositeComponentsWithEveryArgumentAndTransformEncoding()
    {
        byte[] composite = SyntheticTables.CompositeGlyph(
            (0, 0, 100, 100),
            (0x0020 | 0x0002, 1, [5, 6]),
            (0x0020 | 0x0001 | 0x0002, 2, [0, 5, 0, 6]),
            (0x0020 | 0x0008, 3, [1, 2, 0x40, 0]),
            (0x0020 | 0x0040, 4, [1, 2, 0x40, 0, 0x20, 0]),
            (0x0080, 5, [1, 2, 0x40, 0, 0, 0, 0, 0, 0x40, 0]));
        GlyphTable table = Table(false, Outline, composite);
        List<GlyphComponent> components = [];

        table.AddComponents(1, components);

        Assert.Equal(new ushort[] { 1, 2, 3, 4, 5 }, components.Select(component => component.GlyphId));
        Assert.Equal(new[] { 12, 18, 26, 34, 44 }, components.Select(component => component.GlyphIdOffset));
        Assert.True(GlyphTable.IsComposite(table.GetGlyphData(1).Span));
        Assert.False(GlyphTable.IsComposite(table.GetGlyphData(0).Span));
    }

    [Fact]
    public void AddsNoComponentsForASimpleOrEmptyGlyph()
    {
        GlyphTable table = Table(false, Outline, []);
        List<GlyphComponent> components = [];

        table.AddComponents(0, components);
        table.AddComponents(1, components);

        Assert.Empty(components);
    }

    [Fact]
    public void RejectsACompositeWhoseLastRecordClaimsMore()
    {
        byte[] composite = SyntheticTables.CompositeGlyph((0, 0, 1, 1), (0x0020 | 0x0002, 0, [1, 1]));
        GlyphTable table = Table(false, Outline, composite);

        Assert.Throws<FontFormatException>(() => table.AddComponents(1, []));
    }

    [Fact]
    public void RejectsARecordRunningPastTheGlyph()
    {
        byte[] composite = SyntheticTables.CompositeGlyph((0, 0, 1, 1), (0x0080, 0, [1, 1]));
        GlyphTable table = Table(true, Outline, composite);

        Assert.Throws<FontFormatException>(() => table.AddComponents(1, []));
    }

    [Fact]
    public void RejectsLocationsOutOfOrder()
    {
        (byte[] glyf, byte[] loca) = SyntheticTables.GlyphData(false, Outline, Outline);
        BigEndian.WriteUInt16(loca, 2, 100);
        GlyphTable table = new GlyphTable(glyf, loca, 2, 0);

        Assert.Throws<FontFormatException>(() => table.GetGlyphData(1));
    }

    [Fact]
    public void RejectsAGlyphStartingPastTheTable()
    {
        (byte[] glyf, byte[] loca) = SyntheticTables.GlyphData(true, Outline);
        BigEndian.WriteUInt32(loca, 0, 1000);
        BigEndian.WriteUInt32(loca, 4, 1100);

        Assert.Throws<FontFormatException>(() => new GlyphTable(glyf, loca, 1, 1).GetGlyphData(0));
    }

    [Fact]
    public void EndsALastGlyphThatRunsPastTheTableWhereTheTableEnds()
    {
        (byte[] glyf, byte[] loca) = SyntheticTables.GlyphData(true, Outline);
        BigEndian.WriteUInt32(loca, 4, (uint)glyf.Length + 8);

        Assert.Equal(glyf.Length, new GlyphTable(glyf, loca, 1, 1).GetGlyphData(0).Length);
    }

    [Fact]
    public void RejectsAGlyphShorterThanItsHeader()
    {
        (byte[] glyf, byte[] loca) = SyntheticTables.GlyphData(false, Outline, Outline);
        BigEndian.WriteUInt16(loca, 2, 2);

        Assert.Throws<FontFormatException>(() => new GlyphTable(glyf, loca, 2, 0).GetGlyphData(0));
    }

    [Fact]
    public void RejectsALocaTooShortForTheGlyphCount()
    {
        (byte[] glyf, byte[] loca) = SyntheticTables.GlyphData(false, Outline);

        Assert.Throws<FontFormatException>(() => new GlyphTable(glyf, loca, 5, 0));
    }

    [Fact]
    public void RejectsALocationFormatThatDoesNotExist()
    {
        (byte[] glyf, byte[] loca) = SyntheticTables.GlyphData(false, Outline);

        Assert.Throws<FontFormatException>(() => new GlyphTable(glyf, loca, 1, 2));
    }

    [Fact]
    public void RejectsAGlyphOutsideTheFont()
    {
        GlyphTable table = Table(false, Outline);

        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetGlyphData(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SyntheticFont.Minimal().Load().TryGetGlyphBounds(9, out _));
    }

    [Fact]
    public void MeasuresEachGlyphPaddedToTheAlignmentGiven()
    {
        GlyphTable table = Located(32, 0, 13, 32);

        Assert.Equal(16 + 20, table.MeasurePadded([0, 1], 4));
        Assert.Equal(14 + 20, table.MeasurePadded([0, 1], 2));
    }

    [Fact]
    public void MeasuresLocationsItCannotReadAsEmptyRatherThanThrowing()
    {
        // Glyph 1 ends before it starts, glyph 2 runs past the table and glyph 3 starts past it; glyph 9 is not in
        // the font.
        GlyphTable table = Located(48, 0, 16, 8, 100, 120);

        Assert.Equal(16 + 40, table.MeasurePadded([0, 1, 2, 3, 9], 4));
    }

    [Fact]
    public void MeasuresNoMoreThanTheTableAndItsPaddingHoweverTheLocationsOverlap()
    {
        GlyphTable table = Located(48, 0, 48, 0, 48);

        Assert.Equal(48 + (3 * 3), table.MeasurePadded([0, 1, 2], 4));
    }

    [Fact]
    public void ReadsBoundsOfTheCommittedFonts()
    {
        // Values from fontTools' glyf table for the same files.
        Assert.True(TestFonts.Regular.TryGetGlyphBounds(36, out GlyphBounds a));
        Assert.Equal(new GlyphBounds(0, 0, 638, 717), a);
        Assert.True(TestFonts.SpecimenRegular.TryGetGlyphBounds(101, out GlyphBounds nested));
        Assert.Equal(new GlyphBounds(0, 0, 638, 1166), nested);
    }

    [Fact]
    public void ReadsHorizontalMetrics()
    {
        // Two full metrics; the third glyph shares the last advance and has only a bearing, the fourth not even that.
        HorizontalMetricsTable metrics = new HorizontalMetricsTable(
            SyntheticTables.Hmtx([(500, 10), (600, 20)], [30]), numberOfHMetrics: 2, glyphCount: 4);

        ushort[] glyphs = [0, 1, 2, 3];

        Assert.Equal(new ushort[] { 500, 600, 600, 600 }, glyphs.Select(metrics.GetAdvance));
        Assert.Equal(new short[] { 10, 20, 30, 0 }, glyphs.Select(metrics.GetLeftSideBearing));
    }

    [Fact]
    public void RejectsHorizontalMetricsWithoutAnyAdvance()
    {
        Assert.Throws<FontFormatException>(() => new HorizontalMetricsTable(new byte[8], 0, 2));
        Assert.Throws<FontFormatException>(() => new HorizontalMetricsTable(new byte[6], 2, 2));
    }

    [Fact]
    public void ToleratesMoreFullMetricsThanGlyphs()
    {
        HorizontalMetricsTable metrics = new HorizontalMetricsTable(SyntheticTables.Hmtx((500, 1), (600, 2)), 5, 2);

        Assert.Equal(600, metrics.GetAdvance(1));
    }
}
