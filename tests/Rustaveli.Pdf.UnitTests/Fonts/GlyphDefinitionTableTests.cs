using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

public class GlyphDefinitionTableTests
{
    private const ushort Base = 1;
    private const ushort Ligature = 2;
    private const ushort Mark = 3;
    private const ushort OtherMark = 4;
    private const ushort Component = 5;
    private const ushort Unclassified = 6;

    /// <summary>Glyphs 1 to 5 are a base, a ligature, two marks and a component; mark 3 attaches as class 1, mark 4
    /// as class 2; mark set 0 holds mark 3 alone.</summary>
    private static GlyphDefinitionTable Definitions() => new GlyphDefinitionTable(SyntheticLayout.Gdef(
        SyntheticLayout.ClassFormat1(1, 1, 2, 3, 3, 4),
        SyntheticLayout.ClassFormat2((3, 3, 1), (4, 4, 2)),
        [SyntheticLayout.CoverageFormat1(3)]));

    [Fact]
    public void ReadsGlyphClasses()
    {
        GlyphDefinitionTable definitions = Definitions();

        Assert.Equal(GlyphClass.Base, definitions.ClassOf(Base));
        Assert.Equal(GlyphClass.Ligature, definitions.ClassOf(Ligature));
        Assert.Equal(GlyphClass.Mark, definitions.ClassOf(Mark));
        Assert.Equal(GlyphClass.Component, definitions.ClassOf(Component));
        Assert.Equal(GlyphClass.Unclassified, definitions.ClassOf(Unclassified));
        Assert.Equal(1, definitions.MarkAttachmentClassOf(Mark));
        Assert.Equal(2, definitions.MarkAttachmentClassOf(OtherMark));
        Assert.Equal(0, definitions.MarkAttachmentClassOf(Base));
    }

    [Fact]
    public void ClassifiesNothingWithoutClassDefinitions()
    {
        GlyphDefinitionTable definitions = new GlyphDefinitionTable(SyntheticLayout.Gdef(null));

        Assert.Equal(GlyphClass.Unclassified, definitions.ClassOf(Mark));
        Assert.Equal(0, definitions.MarkAttachmentClassOf(Mark));
        Assert.Equal(0, definitions.MarkGlyphSetCount);
        Assert.False(definitions.Skips(Mark, LookupFlags.IgnoreMarks | LookupFlags.IgnoreBaseGlyphs, null));
    }

    [Fact]
    public void ReadsMarkGlyphSets()
    {
        GlyphDefinitionTable definitions = new GlyphDefinitionTable(SyntheticLayout.Gdef(
            null, null, [SyntheticLayout.CoverageFormat1(3), SyntheticLayout.CoverageFormat1(4, 9)]));

        Assert.Equal(2, definitions.MarkGlyphSetCount);
        Assert.Equal(0, definitions.GetMarkGlyphSet(0)!.IndexOf(3));
        Assert.Equal(-1, definitions.GetMarkGlyphSet(0)!.IndexOf(4));
        Assert.Equal(1, definitions.GetMarkGlyphSet(1)!.IndexOf(9));
        Assert.Null(definitions.GetMarkGlyphSet(2));
    }

    [Fact]
    public void ReadsNoMarkGlyphSetsBeforeVersionOnePointTwo()
    {
        // Version 1.0 ends before the mark glyph sets field; the bytes after the header are not read as one.
        byte[] table = SyntheticLayout.Gdef(null, null, [SyntheticLayout.CoverageFormat1(3)]);
        BigEndian.WriteUInt16(table, 2, 0);

        Assert.Equal(0, new GlyphDefinitionTable(table).MarkGlyphSetCount);
    }

    [Fact]
    public void RejectsAVersionThatDoesNotExist()
    {
        byte[] table = SyntheticLayout.Gdef(null);
        BigEndian.WriteUInt16(table, 0, 2);

        FontFormatException error = Assert.Throws<FontFormatException>(() => new GlyphDefinitionTable(table));

        Assert.Contains("GDEF version 2.2", error.Message);
    }

    [Fact]
    public void RejectsMarkGlyphSetsOfAnUnknownFormat()
    {
        byte[] table = SyntheticLayout.Gdef(null, null, [SyntheticLayout.CoverageFormat1(3)]);
        BigEndian.WriteUInt16(table, 14, 2);

        FontFormatException error = Assert.Throws<FontFormatException>(() => new GlyphDefinitionTable(table));

        Assert.Contains("format 2", error.Message);
    }

    [Fact]
    public void RejectsMoreMarkGlyphSetsThanTheTableHolds()
    {
        byte[] table = SyntheticLayout.Gdef(null, null, [SyntheticLayout.CoverageFormat1(3)]);
        BigEndian.WriteUInt16(table, 16, 100);

        Assert.Throws<FontFormatException>(() => new GlyphDefinitionTable(table));
    }

    [Theory]
    [InlineData(0x7FFFFFF0L)]
    [InlineData(0xFFFFFFFFL)]
    public void RejectsAMarkGlyphSetPastTheTable(long offset)
    {
        byte[] table = SyntheticLayout.Gdef(null, null, [SyntheticLayout.CoverageFormat1(3)]);
        BigEndian.WriteUInt32(table, 18, (uint)offset);
        GlyphDefinitionTable definitions = new GlyphDefinitionTable(table);

        Assert.Throws<FontFormatException>(() => definitions.GetMarkGlyphSet(0));
    }

    [Fact]
    public void RejectsAMarkGlyphSetJustPastTheTable()
    {
        byte[] table = SyntheticLayout.Gdef(null, null, [SyntheticLayout.CoverageFormat1(3)]);

        // The set's offset counts from the mark glyph sets table at 14; point it exactly at the table's end.
        BigEndian.WriteUInt32(table, 18, (uint)(table.Length - 14));
        GlyphDefinitionTable definitions = new GlyphDefinitionTable(table);

        Assert.Throws<FontFormatException>(() => definitions.GetMarkGlyphSet(0));
    }

    [Theory]
    [InlineData(Base, (int)LookupFlags.IgnoreBaseGlyphs, true)]
    [InlineData(Base, (int)(LookupFlags.IgnoreLigatures | LookupFlags.IgnoreMarks), false)]
    [InlineData(Ligature, (int)LookupFlags.IgnoreLigatures, true)]
    [InlineData(Ligature, (int)(LookupFlags.IgnoreBaseGlyphs | LookupFlags.IgnoreMarks), false)]
    [InlineData(Mark, (int)LookupFlags.IgnoreMarks, true)]
    [InlineData(Mark, (int)(LookupFlags.IgnoreBaseGlyphs | LookupFlags.IgnoreLigatures), false)]
    [InlineData(Component, 0xFF1E, false)]
    [InlineData(Unclassified, 0xFF1E, false)]
    [InlineData(Mark, 0x0100, false)]
    [InlineData(Mark, 0x0200, true)]
    [InlineData(OtherMark, 0x0200, false)]
    [InlineData(OtherMark, 0x0100, true)]
    [InlineData(Mark, (int)LookupFlags.RightToLeft, false)]
    public void PassesOverGlyphsByClass(ushort glyph, int flags, bool skipped)
    {
        Assert.Equal(skipped, Definitions().Skips(glyph, (LookupFlags)flags, null));
    }

    [Fact]
    public void FiltersMarksBySetAloneWhenASetIsUsed()
    {
        GlyphDefinitionTable definitions = Definitions();
        CoverageTable set = definitions.GetMarkGlyphSet(0)!;

        // Mark 3 is in the set and seen; mark 4 is not and is passed over — whatever attachment type is named.
        Assert.False(definitions.Skips(Mark, LookupFlags.UseMarkFilteringSet | (LookupFlags)0x0200, set));
        Assert.True(definitions.Skips(OtherMark, LookupFlags.UseMarkFilteringSet | (LookupFlags)0x0200, set));
        Assert.False(definitions.Skips(Base, LookupFlags.UseMarkFilteringSet, set));

        // A set the font lacks holds no marks.
        Assert.True(definitions.Skips(Mark, LookupFlags.UseMarkFilteringSet, null));

        // Ignoring marks outright wins over the set.
        Assert.True(definitions.Skips(Mark, LookupFlags.UseMarkFilteringSet | LookupFlags.IgnoreMarks, set));
    }

    [Fact]
    public void ReadsTheDefinitionsOfACommittedFont()
    {
        OpenTypeFont font = TestFonts.Regular;
        GlyphDefinitionTable definitions = font.GlyphDefinitions!;

        // f, the fi ligature and a combining acute, as fontTools reads them.
        Assert.Equal(GlyphClass.Base, definitions.ClassOf(73));
        Assert.Equal(GlyphClass.Ligature, definitions.ClassOf(1654));
        Assert.Equal(GlyphClass.Mark, definitions.ClassOf(2665));
        Assert.Equal(6, definitions.MarkGlyphSetCount);
        Assert.True(definitions.GetMarkGlyphSet(0)!.IndexOf(2663) >= 0);
        Assert.Same(definitions, font.GlyphDefinitions);
    }

    [Fact]
    public void HasNoDefinitionsWithoutTheTable()
    {
        Assert.Null(SyntheticFont.Minimal().Load().GlyphDefinitions);
    }
}
