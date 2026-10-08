using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Output;

namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>
/// Subsetting, checked by reading the subset back. Skia reads the same subsets independently in the integration
/// tests (SubsetSkiaTests).
/// </summary>
public class TrueTypeSubsetterTests
{
    private const ushort NotInTheFont = 60_000;

    private static readonly string[] PdfTables =
        ["cmap", "cvt ", "fpgm", "glyf", "head", "hhea", "hmtx", "loca", "maxp", "post", "prep"];

    private static OpenTypeFont Reparse(TrueTypeSubset subset) => OpenTypeFont.Load(subset.FontData);

    private static IEnumerable<ushort> GlyphsOf(OpenTypeFont font, string text) =>
        text.Select(character => font.GetGlyphId(character));

    private static IEnumerable<ushort> GlyphRange(int first, int count) =>
        Enumerable.Range(first, count).Select(glyph => (ushort)glyph);

    private static List<ushort> Components(OpenTypeFont font, ushort glyph)
    {
        List<GlyphComponent> components = [];
        font.Glyphs!.AddComponents(glyph, components);
        return components.Select(component => component.GlyphId).ToList();
    }

    [Fact]
    public void KeepsNotdefTheGlyphsAskedForAndTheirComponents()
    {
        OpenTypeFont font = TestFonts.Regular;

        // Å (135) is built from A (36) and a ring (335).
        TrueTypeSubset subset = TrueTypeSubsetter.Subset(font, [135]);
        OpenTypeFont result = Reparse(subset);

        Assert.Equal(new ushort[] { 0, 36, 135, 335 }, subset.OriginalGlyphIds);
        Assert.Equal(4, subset.GlyphCount);
        Assert.Equal(4, result.GlyphCount);
        Assert.Equal(new Dictionary<ushort, ushort> { [0] = 0, [36] = 1, [135] = 2, [335] = 3 }, subset.GlyphIdMap);
        Assert.Equal(new ushort[] { 1, 3 }, Components(result, 2));
    }

    [Fact]
    public void PullsInComponentsOfComponents()
    {
        OpenTypeFont font = TestFonts.SpecimenRegular;

        // U+E000 (101) is Å (98) plus an acute (97); Å is A (34) plus a ring (100).
        TrueTypeSubset subset = TrueTypeSubsetter.Subset(font, [font.GetGlyphId('\uE000')]);
        OpenTypeFont result = Reparse(subset);

        Assert.Equal(new ushort[] { 0, 34, 97, 98, 100, 101 }, subset.OriginalGlyphIds);
        Assert.Equal(new ushort[] { 3, 2 }, Components(result, 5));
        Assert.Equal(new ushort[] { 1, 4 }, Components(result, 3));
    }

    [Theory]
    [InlineData('\uE001')]
    [InlineData('\uE002')]
    [InlineData('\uE003')]
    public void KeepsComponentTransformsIntact(char character)
    {
        OpenTypeFont font = TestFonts.SpecimenRegular;
        ushort original = font.GetGlyphId(character);
        TrueTypeSubset subset = TrueTypeSubsetter.Subset(font, [original]);
        OpenTypeFont result = Reparse(subset);

        byte[] before = font.Glyphs!.GetGlyphData(original).ToArray();
        byte[] after = result.Glyphs!.GetGlyphData(subset.GlyphIdMap[original]).ToArray();

        // Only the component's glyph id — A, 34 before and 1 after — may differ.
        Assert.Equal(34, BigEndian.UInt16(before, 12));
        Assert.Equal(1, BigEndian.UInt16(after, 12));
        BigEndian.WriteUInt16(after, 12, 34);
        Assert.Equal(before, after.Take(before.Length));
    }

    [Fact]
    public void PreservesAdvancesAndCharacterMapping()
    {
        OpenTypeFont font = TestFonts.Regular;
        const string Text = "Hello, World! \u00C5";
        TrueTypeSubset subset = TrueTypeSubsetter.Subset(font, GlyphsOf(font, Text));
        OpenTypeFont result = Reparse(subset);

        foreach (char character in Text)
        {
            ushort original = font.GetGlyphId(character);
            ushort renumbered = result.GetGlyphId(character);
            short bearing = font.HorizontalMetrics.GetLeftSideBearing(original);

            Assert.Equal(subset.GlyphIdMap[original], renumbered);
            Assert.Equal(font.GetAdvance(original), result.GetAdvance(renumbered));
            Assert.Equal(bearing, result.HorizontalMetrics.GetLeftSideBearing(renumbered));
        }

        Assert.Equal(font.GetAdvance(0), result.GetAdvance(0));
        Assert.Equal(font.UnitsPerEm, result.UnitsPerEm);
        Assert.Equal(font.LineMetrics.Ascent, result.HorizontalHeader.Ascender);
    }

    [Fact]
    public void IsAFractionOfTheFont()
    {
        OpenTypeFont font = TestFonts.Regular;
        TrueTypeSubset subset = TrueTypeSubsetter.Subset(font, GlyphsOf(font, "Invoice 2026"));
        int size = subset.FontData.Length;

        Assert.True(size < font.FileData.Length / 50, $"The subset is {size} bytes.");
    }

    [Fact]
    public void KeepsOnlyTheTablesAPdfNeedsAndTheHintingProgramsWhenAsked()
    {
        TrueTypeSubset subset = TrueTypeSubsetter.Subset(TestFonts.Regular, [36], keepHinting: true);
        TableDirectory directory = Reparse(subset).Tables;

        Assert.Equal(PdfTables, directory.Records.Select(record => TableTag.ToString(record.Tag)));
        Assert.True(TestFonts.Regular.TryGetTable(TableTag.Fpgm, out ReadOnlyMemory<byte> fpgm));
        Assert.True(Reparse(subset).TryGetTable(TableTag.Fpgm, out ReadOnlyMemory<byte> copied));
        Assert.Equal(fpgm.ToArray(), copied.ToArray());
    }

    [Fact]
    public void WritesAValidTableDirectoryAndChecksums()
    {
        byte[] data = TrueTypeSubsetter.Subset(TestFonts.Italic, [36, 57], keepHinting: true).FontData;
        int count = BigEndian.UInt16(data, 4);

        // searchRange is 16 times the largest power of two at most the table count; eleven tables give 8.
        Assert.Equal(11, count);
        Assert.Equal(128, BigEndian.UInt16(data, 6));
        Assert.Equal(3, BigEndian.UInt16(data, 8));
        Assert.Equal(48, BigEndian.UInt16(data, 10));

        uint previousTag = 0;

        for (int index = 0; index < count; index++)
        {
            int record = 12 + (16 * index);
            uint tag = BigEndian.UInt32(data, record);
            uint checksum = BigEndian.UInt32(data, record + 4);
            int offset = (int)BigEndian.UInt32(data, record + 8);
            int length = (int)BigEndian.UInt32(data, record + 12);
            byte[] table = data.Skip(offset).Take(length).ToArray();

            if (tag == TableTag.Head)
                BigEndian.WriteUInt32(table, 8, 0);

            Assert.True(tag > previousTag, "Tables must be sorted by tag.");
            Assert.Equal(0, offset % 4);
            Assert.Equal(Checksum(table), checksum);
            previousTag = tag;
        }

        Assert.Equal(0xB1B0AFBA, Checksum(data));
        Assert.Equal(0, data.Length % 4);
    }

    [Fact]
    public void UsesShortLocationsUntilTheOutlinesOutgrowThem()
    {
        OpenTypeFont font = TestFonts.Regular;
        TrueTypeSubset small = TrueTypeSubsetter.Subset(font, [36]);
        TrueTypeSubset whole = TrueTypeSubsetter.Subset(font, GlyphRange(0, font.GlyphCount));

        Assert.Equal(0, Reparse(small).Head.IndexToLocFormat);
        Assert.Equal(1, Reparse(whole).Head.IndexToLocFormat);
        Assert.Equal(font.GlyphCount, Reparse(whole).GlyphCount);
        Assert.True(Reparse(whole).TryGetGlyphBounds(1000, out GlyphBounds bounds));
        Assert.True(font.TryGetGlyphBounds(1000, out GlyphBounds original));
        Assert.Equal(original, bounds);
    }

    [Fact]
    public void StoresTrailingEqualAdvancesOnce()
    {
        OpenTypeFont font = TestFonts.SpecimenRegular;
        TrueTypeSubset subset = TrueTypeSubsetter.Subset(font, GlyphsOf(font, "\uE000\uE001\uE002\uE003"));
        OpenTypeFont result = Reparse(subset);

        // Advances 600 (.notdef), 639, 281, 639, 300, then 639 four times: the last four share one advance.
        Assert.Equal(new ushort[] { 0, 34, 97, 98, 100, 101, 102, 103, 104 }, subset.OriginalGlyphIds);
        Assert.Equal(6, result.HorizontalHeader.NumberOfHMetrics);

        for (ushort glyph = 0; glyph < subset.GlyphCount; glyph++)
            Assert.Equal(font.GetAdvance(subset.OriginalGlyphIds[glyph]), result.GetAdvance(glyph));
    }

    [Fact]
    public void RecomputesTheFontWideExtremesOverTheGlyphsKept()
    {
        OpenTypeFont font = TestFonts.Regular;
        TrueTypeSubset subset = TrueTypeSubsetter.Subset(font, GlyphsOf(font, "Hello, World! \u00C5"));
        OpenTypeFont result = Reparse(subset);

        // Values fontTools computes for the same subset.
        Assert.Equal(new GlyphBounds(0, -129, 917, 878), result.Descriptor.BoundingBox);
        Assert.Equal(930, result.HorizontalHeader.AdvanceWidthMax);
    }

    [Fact]
    public void WritesAVersion3PostWithTheOriginalHeader()
    {
        OpenTypeFont result = Reparse(TrueTypeSubsetter.Subset(TestFonts.Italic, [36]));

        Assert.True(result.TryGetTable(TableTag.Post, out ReadOnlyMemory<byte> post));
        Assert.Equal(32, post.Length);
        Assert.Equal(0x00030000u, BigEndian.UInt32(post.Span, 0));
        Assert.Equal(-12f, result.Post!.ItalicAngle);
        Assert.Equal(-100, result.Post.UnderlinePosition);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SubsetsAFontWithoutOptionalTables(bool keepHinting)
    {
        OpenTypeFont font = SyntheticFont.Minimal().Load();
        OpenTypeFont result = Reparse(TrueTypeSubsetter.Subset(font, [2], keepHinting));

        Assert.Equal(2, result.GlyphCount);
        Assert.Equal(1, result.GetGlyphId('B'));
        Assert.Equal(0, result.GetGlyphId('A'));
        Assert.Equal(0f, result.Post!.ItalicAngle);
        Assert.False(result.TryGetTable(TableTag.Fpgm, out _));
    }

    [Fact]
    public void WritesAPostWithAnEmptyHeaderForAFontWhosePostIsCutShort()
    {
        // The version and italic angle alone: a header cut short is not copied a part at a time.
        OpenTypeFont font = SyntheticFont.Minimal().With("post", SyntheticTables.Post(italicAngle: -12).Take(8).ToArray()).Load();
        OpenTypeFont result = Reparse(TrueTypeSubsetter.Subset(font, [2]));

        Assert.Equal(0f, result.Post!.ItalicAngle);
        Assert.Equal(0, result.Post.UnderlinePosition);
    }

    [Fact]
    public void KeepsASymbolFontsCodes()
    {
        OpenTypeFont font = SyntheticFont.Minimal()
            .With("cmap", SyntheticTables.Cmap((3, 0, SyntheticTables.Format4((0xF041, 1), (0xF042, 2)))))
            .Load();

        OpenTypeFont result = Reparse(TrueTypeSubsetter.Subset(font, [2]));

        Assert.Equal(CharacterEncoding.Symbol, result.CharacterMap.Encoding);
        Assert.Equal(1, result.GetGlyphId(0xF042));
        Assert.Equal(1, result.GetGlyphId('B'));
    }

    [Fact]
    public void MapsCharactersBeyondTheBasicPlane()
    {
        byte[] groups = SyntheticTables.Format12(false, ('A', 'A', 1), (0x1D400, 0x1D400, 2));
        OpenTypeFont font = SyntheticFont.Minimal().With("cmap", SyntheticTables.Cmap((3, 10, groups))).Load();

        OpenTypeFont result = Reparse(TrueTypeSubsetter.Subset(font, [1, 2]));

        Assert.Equal(1, result.GetGlyphId('A'));
        Assert.Equal(2, result.GetGlyphId(0x1D400));
    }

    [Fact]
    public void FallsBackToFormat12WhenFormat4WouldOverflow()
    {
        // Nine thousand glyphs mapped from every other code point: one format 4 segment each would pass 64 KB.
        const int Glyphs = 9000;
        byte[][] empty = Enumerable.Repeat(Array.Empty<byte>(), Glyphs).ToArray();
        (byte[] glyf, byte[] loca) = SyntheticTables.GlyphData(false, empty);
        (long, long, long)[] groups = GlyphRange(1, Glyphs - 1)
            .Select(glyph => (2L * glyph, 2L * glyph, (long)glyph))
            .ToArray();
        OpenTypeFont font = SyntheticFont.Minimal()
            .With("maxp", SyntheticTables.Maxp(Glyphs))
            .With("hhea", SyntheticTables.Hhea(numberOfHMetrics: 1))
            .With("hmtx", SyntheticTables.Hmtx((500, 0)))
            .With("cmap", SyntheticTables.Cmap((3, 10, SyntheticTables.Format12(false, groups))))
            .With("glyf", glyf)
            .With("loca", loca)
            .Load();

        OpenTypeFont result = Reparse(TrueTypeSubsetter.Subset(font, GlyphRange(1, Glyphs - 1)));

        Assert.True(result.TryGetTable(TableTag.Cmap, out ReadOnlyMemory<byte> cmap));
        Assert.Equal(1, BigEndian.UInt16(cmap.Span, 2));
        Assert.Equal(10, BigEndian.UInt16(cmap.Span, 6));
        Assert.Equal(4321, result.GetGlyphId(8642));
        Assert.Equal(0, result.GetGlyphId(8643));
    }

    [Fact]
    public void NumbersGlyphsInTheOrderGiven()
    {
        OpenTypeFont font = TestFonts.Regular;
        TrueTypeSubset subset = TrueTypeSubsetter.SubsetInOrder(font, [0, 135, 57]);

        // Å's components come after the glyphs listed, in the order Å references them.
        Assert.Equal(new ushort[] { 0, 135, 57, 36, 335 }, subset.OriginalGlyphIds);
        Assert.Equal(new ushort[] { 3, 4 }, Components(Reparse(subset), 1));
        Assert.Equal(
            new Dictionary<ushort, ushort> { [0] = 0, [135] = 1, [57] = 2, [36] = 3, [335] = 4 }, subset.GlyphIdMap);
    }

    [Fact]
    public void RejectsANumberingThatDoesNotStartWithNotdefOrRepeatsAGlyph()
    {
        Assert.Throws<ArgumentException>(() => TrueTypeSubsetter.SubsetInOrder(TestFonts.Regular, [36]));
        Assert.Throws<ArgumentException>(() => TrueTypeSubsetter.SubsetInOrder(TestFonts.Regular, []));
        Assert.Throws<ArgumentException>(() => TrueTypeSubsetter.SubsetInOrder(TestFonts.Regular, [0, 36, 36]));
    }

    [Fact]
    public void ReportsARepeatedGlyphBeforeAnyProblemWithTheFont()
    {
        // A repeat is reported though a glyph listed before it is not in the font, and though the font's outlines
        // cannot be subset at all.
        ArgumentException outside = Assert.Throws<ArgumentException>(
            () => TrueTypeSubsetter.SubsetInOrder(TestFonts.Regular, [0, NotInTheFont, 36, 36]));
        Assert.Equal("numbering", outside.ParamName);
        Assert.Throws<ArgumentException>(() => TrueTypeSubsetter.SubsetInOrder(TestFonts.Cff, [0, 34, 34]));
    }

    [Fact]
    public void ReportsOutlinesItCannotSubsetBeforeAGlyphOutsideTheFont()
    {
        Assert.Throws<NotSupportedException>(() => TrueTypeSubsetter.Subset(TestFonts.Cff, [NotInTheFont]));
        Assert.Throws<NotSupportedException>(() => TrueTypeSubsetter.SubsetInOrder(TestFonts.Cff, [0, NotInTheFont]));
    }

    [Fact]
    public void ReportsTheFirstGlyphListedOutsideTheFont()
    {
        ArgumentOutOfRangeException first = Assert.Throws<ArgumentOutOfRangeException>(
            () => TrueTypeSubsetter.Subset(TestFonts.Regular, [36, NotInTheFont + 1, 36, NotInTheFont]));
        Assert.Equal((ushort)(NotInTheFont + 1), first.ActualValue);
    }

#if NET
    [Fact]
    public void SubsettingForADocumentNumbersItsGlyphsInOneMap()
    {
        // Allocation budget: every export subsets each face it embeds, from the numbering its pages were written
        // with. One map numbers the glyphs, finds a glyph listed twice and is the subset's own. A set to find repeats,
        // a flag for every glyph in the font and a second copy of the map cost this subset 5 KB more. What is left
        // is the subset font and the tables it is built from. When a change moves this on purpose, set the new
        // figure and say why in the commit.
        const long Budget = 18_360;
        OpenTypeFont font = TestFonts.Regular;
        ushort[] numbering =
            [0, .. GlyphsOf(font, "Invoice 2026, Total due: 1,250.00 EUR").Distinct().Where(glyph => glyph != 0)];
        TrueTypeSubsetter.SubsetInOrder(font, numbering);

        long before = GC.GetAllocatedBytesForCurrentThread();
        TrueTypeSubsetter.SubsetInOrder(font, numbering);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated <= Budget, $"Subsetting allocated {allocated} bytes; its budget is {Budget}.");
    }
#endif

    [Fact]
    public void RejectsGlyphsOutsideTheFont()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TrueTypeSubsetter.Subset(SyntheticFont.Minimal().Load(), [3]));
    }

    [Fact]
    public void CannotSubsetCffOutlines()
    {
        NotSupportedException exception =
            Assert.Throws<NotSupportedException>(() => TrueTypeSubsetter.Subset(TestFonts.Cff, [34]));

        Assert.Contains("Cff", exception.Message);
    }

    [Fact]
    public void RejectsAComponentThatDoesNotExist()
    {
        OpenTypeFont font = FontWithGlyphs(
            SyntheticTables.SimpleGlyph(0, 0, 10, 10),
            SyntheticTables.CompositeGlyph((0, 0, 10, 10), (0x0002, 7, [0, 0])));

        Assert.Throws<FontFormatException>(() => TrueTypeSubsetter.Subset(font, [1]));
    }

    [Fact]
    public void EndsACycleOfCompositesThatReferenceEachOther()
    {
        OpenTypeFont font = FontWithGlyphs(
            SyntheticTables.SimpleGlyph(0, 0, 10, 10),
            SyntheticTables.CompositeGlyph((0, 0, 10, 10), (0x0002, 2, [0, 0])),
            SyntheticTables.CompositeGlyph((0, 0, 10, 10), (0x0002, 1, [0, 0])));

        TrueTypeSubset subset = TrueTypeSubsetter.Subset(font, [1]);

        Assert.Equal(new ushort[] { 0, 1, 2 }, subset.OriginalGlyphIds);
    }

    [Fact]
    public void DerivesAStableTagFromTheGlyphsKept()
    {
        TrueTypeSubset first = TrueTypeSubsetter.Subset(TestFonts.Regular, [36, 57]);
        TrueTypeSubset again = TrueTypeSubsetter.Subset(TestFonts.Regular, [57, 36]);
        TrueTypeSubset other = TrueTypeSubsetter.Subset(TestFonts.Regular, [36, 58]);

        Assert.Matches("^[A-Z]{6}$", first.Tag);
        Assert.Equal(first.Tag, again.Tag);
        Assert.NotEqual(first.Tag, other.Tag);
        Assert.Equal(first.FontData, again.FontData);
    }

    [Fact]
    public void ReportsWhetherAGlyphIsInTheSubset()
    {
        TrueTypeSubset subset = TrueTypeSubsetter.Subset(TestFonts.Regular, [36]);

        Assert.True(subset.TryGetSubsetGlyphId(36, out ushort renumbered));
        Assert.Equal(1, renumbered);
        Assert.False(subset.TryGetSubsetGlyphId(57, out _));
    }

    [Fact]
    public void NumbersGlyphsByFirstUse()
    {
        OpenTypeFont font = TestFonts.Regular;
        GlyphSubset glyphs = new GlyphSubset(font);

        Assert.Same(font, glyphs.Font);
        Assert.Equal(1, glyphs.Add(font.GetGlyphId('V'), 'V'));
        Assert.Equal(2, glyphs.Add(font.GetGlyphId('A')));
        Assert.Equal(2, glyphs.Add(font.GetGlyphId('A'), 'A'));
        Assert.Equal(1, glyphs.Add(font.GetGlyphId('V')));
        Assert.Equal(0, glyphs.Add(0, 'X'));
        Assert.Equal(3, glyphs.Count);
        Assert.Equal(new ushort[] { 0, 57, 36 }, glyphs.OriginalGlyphIds);
        Assert.Empty(glyphs.SharedCodes);

        Assert.True(glyphs.TryGetText(1, out string v));
        Assert.Equal("V", v);
        Assert.True(glyphs.TryGetText(2, out string a));
        Assert.Equal("A", a);
        Assert.False(glyphs.TryGetText(0, out _));
        Assert.False(glyphs.TryGetText(9, out _));

        TrueTypeSubset subset = glyphs.Build();
        Assert.Equal(new ushort[] { 0, 57, 36 }, subset.OriginalGlyphIds);
    }

    [Fact]
    public void AGlyphShownAsOtherTextGetsACodeOfItsOwn()
    {
        OpenTypeFont font = TestFonts.Regular;
        GlyphSubset glyphs = new GlyphSubset(font);
        ushort v = font.GetGlyphId('V');

        // The V glyph, shown first as V, then as W twice and as a ligature's text: two further codes, each kept.
        Assert.Equal(1, glyphs.Add(v, 'V'));
        Assert.Equal(GlyphSubset.FirstSharedCode, glyphs.Add(v, 'W'));
        Assert.Equal(GlyphSubset.FirstSharedCode, glyphs.Add(v, "W"));
        Assert.Equal(GlyphSubset.FirstSharedCode + 1, glyphs.Add(v, "VV"));
        Assert.Equal(1, glyphs.Add(v, 'V'));

        Assert.Equal(2, glyphs.Count);
        Assert.Equal([((ushort)1, "W"), ((ushort)1, "VV")], glyphs.SharedCodes);
        Assert.True(glyphs.TryGetText(GlyphSubset.FirstSharedCode + 1, out string ligature));
        Assert.Equal("VV", ligature);
        Assert.False(glyphs.TryGetText(GlyphSubset.FirstSharedCode + 2, out _));
    }

    [Fact]
    public void AGlyphStandingForNothingAndForItselfGetsACodeForEach()
    {
        OpenTypeFont font = TestFonts.Regular;
        GlyphSubset glyphs = new GlyphSubset(font);
        ushort acute = font.GetGlyphId('́');

        // A combining acute first drawn as the second glyph of a decomposed letter, then typed on its own.
        ushort silent = glyphs.Add(acute, string.Empty);
        ushort typed = glyphs.Add(acute, '́');

        Assert.NotEqual(silent, typed);
        Assert.True(glyphs.TryGetText(silent, out string nothing));
        Assert.Empty(nothing);
        Assert.True(glyphs.TryGetText(typed, out string itself));
        Assert.Equal("́", itself);
        Assert.Equal(silent, glyphs.Add(acute, string.Empty));
    }

    [Fact]
    public void AGlyphFirstUsedWithoutTextTakesTheFirstTextShown()
    {
        OpenTypeFont font = TestFonts.Regular;
        GlyphSubset glyphs = new GlyphSubset(font);
        ushort a = font.GetGlyphId('A');

        Assert.Equal(1, glyphs.Add(a));
        Assert.Equal(1, glyphs.Add(a, 'A'));
        Assert.Empty(glyphs.SharedCodes);
        Assert.True(glyphs.TryGetText(1, out string text));
        Assert.Equal("A", text);
    }

    [Fact]
    public void ACharacterBeyondTheBasicPlaneIsComparedWhole()
    {
        OpenTypeFont font = TestFonts.Regular;
        GlyphSubset glyphs = new GlyphSubset(font);
        ushort a = font.GetGlyphId('A');

        Assert.Equal(1, glyphs.Add(a, 0x1D400));
        Assert.Equal(1, glyphs.Add(a, 0x1D400));
        Assert.Equal(GlyphSubset.FirstSharedCode, glyphs.Add(a, 'A'));
    }

    [Fact]
    public void TheCodeMapSendsEachCodeToItsGlyph()
    {
        byte[] map = EmbeddedFont.CidToGidMapOf(3, [((ushort)1, "W"), ((ushort)2, "x")]);

        Assert.Equal(2 * (GlyphSubset.FirstSharedCode + 2), map.Length);
        Assert.Equal([0, 0, 0, 1, 0, 2], map.Take(6));
        Assert.All(map.Skip(6).Take((2 * GlyphSubset.FirstSharedCode) - 6), value => Assert.Equal(0, value));
        Assert.Equal([0, 1, 0, 2], map.Skip(2 * GlyphSubset.FirstSharedCode));
    }

    [Fact]
    public void ReportsNoCodepointForAGlyphAddedWithoutOne()
    {
        GlyphSubset glyphs = new GlyphSubset(TestFonts.Regular);
        glyphs.Add(36);

        Assert.False(glyphs.TryGetText(1, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => glyphs.Add(NotInTheFont));
    }

    [Fact]
    public void NumbersEachGlyphOnceUnderConcurrentUse()
    {
        GlyphSubset glyphs = new GlyphSubset(TestFonts.Regular);

        Parallel.For(0, 64, worker =>
        {
            for (int glyph = 1; glyph <= 500; glyph++)
                glyphs.Add((ushort)(((glyph * 7) + worker) % 500 + 1));
        });

        IReadOnlyList<ushort> originals = glyphs.OriginalGlyphIds;

        Assert.Equal(501, glyphs.Count);
        Assert.Equal(originals.Count, originals.Distinct().Count());

        for (int number = 0; number < originals.Count; number++)
            Assert.Equal(number, glyphs.Add(originals[number]));
    }

    private static OpenTypeFont FontWithGlyphs(params byte[][] glyphs)
    {
        (byte[] glyf, byte[] loca) = SyntheticTables.GlyphData(false, glyphs);

        return SyntheticFont.Minimal()
            .With("maxp", SyntheticTables.Maxp(glyphs.Length))
            .With("hhea", SyntheticTables.Hhea(numberOfHMetrics: 1))
            .With("hmtx", SyntheticTables.Hmtx((500, 0)))
            .With("glyf", glyf)
            .With("loca", loca)
            .Load();
    }

    /// <summary>The OpenType checksum, computed independently of the library's writer.</summary>
    private static uint Checksum(byte[] data)
    {
        uint sum = 0;

        for (int position = 0; position < data.Length; position += 4)
        {
            uint word = 0;

            for (int index = 0; index < 4; index++)
                word = (word << 8) | (position + index < data.Length ? data[position + index] : 0u);

            sum += word;
        }

        return sum;
    }
}
