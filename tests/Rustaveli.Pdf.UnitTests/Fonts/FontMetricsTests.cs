using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>Line metrics, style, embedding permissions and the font descriptor, over hand-built tables.</summary>
public class FontMetricsTests
{
    private static OpenTypeFont WithOs2(byte[] os2) => SyntheticFont.Minimal().With("OS/2", os2).Load();

    [Fact]
    public void UsesHheaWhenTheFontDoesNotAskForTypographicMetrics()
    {
        OpenTypeFont font = WithOs2(SyntheticTables.Os2(typo: (700, -300, 90), win: (1100, 400)));

        Assert.Equal(new LineMetrics(800, 200, 0, LineMetricsSource.HorizontalHeader), font.LineMetrics);
    }

    [Fact]
    public void UsesTypographicMetricsWhenTheFontAsksForThem()
    {
        OpenTypeFont font = WithOs2(SyntheticTables.Os2(fsSelection: 0x80, typo: (700, -300, 90), win: (1100, 400)));

        Assert.Equal(new LineMetrics(700, 300, 90, LineMetricsSource.Typographic), font.LineMetrics);
        Assert.Equal(1090, font.LineMetrics.LineHeight);
    }

    [Fact]
    public void FallsBackToTypographicThenWindowsMetricsWhenHheaIsEmpty()
    {
        SyntheticFont font = SyntheticFont.Minimal().With("hhea", SyntheticTables.Hhea(0, 0, 0, numberOfHMetrics: 3));

        LineMetrics typographic = font.With("OS/2", SyntheticTables.Os2(typo: (700, -300, 90), win: (1100, 400)))
            .Load().LineMetrics;
        LineMetrics windows = font.With("OS/2", SyntheticTables.Os2(win: (1100, 400))).Load().LineMetrics;
        LineMetrics nothing = font.Without("OS/2").Load().LineMetrics;

        Assert.Equal(new LineMetrics(700, 300, 90, LineMetricsSource.Typographic), typographic);
        Assert.Equal(new LineMetrics(1100, 400, 0, LineMetricsSource.Windows), windows);
        Assert.Equal(new LineMetrics(0, 0, 0, LineMetricsSource.HorizontalHeader), nothing);
    }

    [Fact]
    public void TakesTheMagnitudeOfAPositiveDescenderAndClampsANegativeGap()
    {
        OpenTypeFont font = SyntheticFont.Minimal()
            .With("hhea", SyntheticTables.Hhea(900, 250, -40, numberOfHMetrics: 3))
            .Load();

        Assert.Equal(new LineMetrics(900, 250, 0, LineMetricsSource.HorizontalHeader), font.LineMetrics);
    }

    [Fact]
    public void ReadsAShortVersionZeroOs2WithoutLineMetrics()
    {
        OpenTypeFont font = WithOs2(SyntheticTables.Os2(version: 0, weight: 700, fsSelection: 0x80, length: 68));

        Assert.False(font.Os2!.HasLineMetrics);
        Assert.Equal(LineMetricsSource.HorizontalHeader, font.LineMetrics.Source);
        Assert.Equal(700, font.Style.Weight);
        Assert.Equal(0, font.Os2.XHeight);
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(2, 86)]
    public void IgnoresVersionTwoFieldsInAnOlderOrTruncatedTable(int version, int? length)
    {
        // The second: a version 2 table that ends where version 1 does, before the fields version 2 added.
        OpenTypeFont font = WithOs2(SyntheticTables.Os2(version: version, xHeight: 500, capHeight: 700, length: length));

        Assert.Equal(0, font.Os2!.XHeight);
        Assert.Equal(0, font.Os2.CapHeight);
    }

    [Fact]
    public void RejectsAnOs2TableShorterThanAnyVersion()
    {
        OpenTypeFont font = WithOs2(SyntheticTables.Os2(length: 60));

        Assert.Throws<FontFormatException>(() => font.Os2);
        Assert.Throws<FontFormatException>(() => font.LineMetrics);
    }

    // Slants as integers: an internal enum cannot appear in a public test method's signature.
    [Theory]
    [InlineData(400, 5, 0, 0, 400, 5, 0)]
    [InlineData(4, 5, 0, 0, 400, 5, 0)]
    [InlineData(9, 5, 0, 0, 900, 5, 0)]
    [InlineData(1200, 5, 0, 0, 1000, 5, 0)]
    [InlineData(0, 5, 0, 1, 700, 5, 0)]
    [InlineData(0, 5, 0, 0, 400, 5, 0)]
    [InlineData(400, 0, 0, 0, 400, 5, 0)]
    [InlineData(400, 12, 0, 0, 400, 5, 0)]
    [InlineData(400, 3, 0x0001, 0, 400, 3, 1)]
    [InlineData(400, 5, 0x0201, 0, 400, 5, 2)]
    [InlineData(400, 5, 0, 2, 400, 5, 1)]
    public void ReadsTheStyle(int weight, int width, int fsSelection, int macStyle, int expectedWeight,
        int expectedWidth, int expectedSlant)
    {
        OpenTypeFont font = SyntheticFont.Minimal()
            .With("head", SyntheticTables.Head(macStyle: macStyle))
            .With("OS/2", SyntheticTables.Os2(weight: weight, width: width, fsSelection: fsSelection))
            .Load();

        Assert.Equal(new FaceStyle(expectedWeight, expectedWidth, (FontSlant)expectedSlant), font.Style);
    }

    [Theory]
    [InlineData(0, 0, 400)]
    [InlineData(1, 0, 700)]
    [InlineData(2, 1, 400)]
    [InlineData(3, 1, 700)]
    public void ReadsTheStyleFromHeadWithoutOs2(int macStyle, int slant, int weight)
    {
        OpenTypeFont font = SyntheticFont.Minimal().With("head", SyntheticTables.Head(macStyle: macStyle)).Load();

        Assert.Null(font.Os2);
        Assert.Equal(new FaceStyle(weight, 5, (FontSlant)slant), font.Style);
        Assert.Equal(weight == 700, font.Head.IsBold);
    }

    [Theory]
    [InlineData(0x0000, true, false, false, false, true, true)]
    [InlineData(0x0002, false, true, false, false, true, false)]
    [InlineData(0x0004, false, false, true, false, true, true)]
    [InlineData(0x0008, false, false, false, true, true, true)]
    [InlineData(0x000E, false, false, false, true, true, true)]
    [InlineData(0x0006, false, false, true, false, true, true)]
    [InlineData(0x0104, false, false, true, false, false, true)]
    [InlineData(0x0200, true, false, false, false, true, false)]
    public void ReportsEmbeddingPermissions(int fsType, bool installable, bool restricted, bool previewAndPrint,
        bool editable, bool subsetting, bool embedding)
    {
        FontEmbedding permissions = WithOs2(SyntheticTables.Os2(fsType: fsType)).Embedding;

        Assert.Equal(fsType, permissions.FsType);
        Assert.Equal(installable, permissions.IsInstallable);
        Assert.Equal(restricted, permissions.IsRestricted);
        Assert.Equal(previewAndPrint, permissions.IsPreviewAndPrint);
        Assert.Equal(editable, permissions.IsEditable);
        Assert.Equal(subsetting, permissions.AllowsSubsetting);
        Assert.Equal(embedding, permissions.AllowsEmbedding);
        Assert.Equal(fsType == 0x0200, permissions.IsBitmapOnly);
    }

    [Fact]
    public void AFontWithoutOs2DeclaresNoRestriction()
    {
        Assert.True(SyntheticFont.Minimal().Load().Embedding.IsInstallable);
    }

    [Fact]
    public void ReadsThePostTable()
    {
        OpenTypeFont font = SyntheticFont.Minimal()
            .With("post", SyntheticTables.Post(-11.5, -120, 60, fixedPitch: true))
            .Load();

        Assert.Equal(-11.5f, font.Post!.ItalicAngle);
        Assert.Equal(-120, font.Post.UnderlinePosition);
        Assert.Equal(60, font.Post.UnderlineThickness);
        Assert.True(font.Post.IsFixedPitch);
        Assert.Throws<FontFormatException>(() =>
            SyntheticFont.Minimal().With("post", new byte[20]).Load().Post);
    }

    // Flags as their PDF bit values: 1 fixed pitch, 2 serif, 8 script.
    [Theory]
    [InlineData(0x0100, null, 2)]
    [InlineData(0x0700, null, 2)]
    [InlineData(0x0800, null, 0)]
    [InlineData(0x0A00, null, 8)]
    [InlineData(0, new byte[] { 2, 2, 0, 0, 0, 0, 0, 0, 0, 0 }, 2)]
    [InlineData(0, new byte[] { 2, 11, 0, 0, 0, 0, 0, 0, 0, 0 }, 0)]
    [InlineData(0, new byte[] { 3, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 8)]
    [InlineData(0, new byte[] { 2, 0, 0, 9, 0, 0, 0, 0, 0, 0 }, 1)]
    [InlineData(0x0800, new byte[] { 2, 2, 0, 0, 0, 0, 0, 0, 0, 0 }, 0)]
    public void DerivesDescriptorFlagsFromTheFamilyClassAndPanose(int familyClass, byte[]? panose, int expected)
    {
        OpenTypeFont font = WithOs2(SyntheticTables.Os2(familyClass: familyClass, panose: panose));

        // "A" and "B" are standard Latin, so the font is nonsymbolic.
        Assert.Equal((FontFlags)expected | FontFlags.Nonsymbolic, font.Descriptor.Flags);
    }

    [Fact]
    public void FlagsFixedPitchItalicAndSymbolicFonts()
    {
        OpenTypeFont font = SyntheticFont.Minimal()
            .With("post", SyntheticTables.Post(-10, fixedPitch: true))
            .With("cmap", SyntheticTables.Cmap((3, 0, SyntheticTables.Format4((0xF041, 1)))))
            .Load();

        Assert.Equal(FontFlags.FixedPitch | FontFlags.Italic | FontFlags.Symbolic, font.Descriptor.Flags);
    }

    [Fact]
    public void CallsAFontSymbolicOnceItMapsBeyondStandardLatin()
    {
        FontFlags latin = SyntheticFont.Minimal()
            .With("cmap", SyntheticTables.Cmap((3, 1, SyntheticTables.Format4(('A', 1), ('\u20AC', 2), ('\uFB02', 2)))))
            .Load().Descriptor.Flags;
        FontFlags greek = SyntheticFont.Minimal()
            .With("cmap", SyntheticTables.Cmap((3, 1, SyntheticTables.Format4(('A', 1), ('\u03A9', 2)))))
            .Load().Descriptor.Flags;
        FontFlags unmapped = SyntheticFont.Minimal()
            .With("cmap", SyntheticTables.Cmap((2, 0, SyntheticTables.Format4(('A', 1)))))
            .Load().Descriptor.Flags;

        Assert.Equal(FontFlags.Nonsymbolic, latin);
        Assert.Equal(FontFlags.Symbolic, greek);
        Assert.Equal(FontFlags.Symbolic, unmapped);
    }

    [Fact]
    public void KnowsEveryCharacterOfTheStandardLatinSet()
    {
        // The characters outside Latin-1 that the standard, Mac Roman, WinAnsi and PDFDoc encodings add.
        int[] extras =
        [
            0x0131, 0x0141, 0x0142, 0x0152, 0x0153, 0x0160, 0x0161, 0x0178, 0x017D, 0x017E, 0x0192, 0x02C6, 0x02C7,
            0x02D8, 0x02D9, 0x02DA, 0x02DB, 0x02DC, 0x02DD, 0x2013, 0x2014, 0x2018, 0x2019, 0x201A, 0x201C, 0x201D,
            0x201E, 0x2020, 0x2021, 0x2022, 0x2026, 0x2030, 0x2039, 0x203A, 0x2044, 0x20AC, 0x2122, 0x2212, 0xFB01,
            0xFB02
        ];
        (int, int)[] mappings = [.. extras.Select(code => (code, 1)), (0x20, 1), (0xFF, 2)];

        foreach (int outsider in new[] { 0x0100, 0x0400, 0x2015, 0xFB03 })
        {
            OpenTypeFont latin = SyntheticFont.Minimal()
                .With("cmap", SyntheticTables.Cmap((3, 1, SyntheticTables.Format4(mappings))))
                .Load();
            OpenTypeFont beyond = SyntheticFont.Minimal()
                .With("cmap", SyntheticTables.Cmap((3, 1, SyntheticTables.Format4([.. mappings, (outsider, 2)]))))
                .Load();

            Assert.Equal(FontFlags.Nonsymbolic, latin.Descriptor.Flags);
            Assert.Equal(FontFlags.Symbolic, beyond.Descriptor.Flags);
        }
    }

    [Fact]
    public void MeasuresCapAndXHeightFromGlyphsWhenOs2LacksThem()
    {
        (byte[] glyf, byte[] loca) = SyntheticTables.GlyphData(
            longOffsets: true,
            SyntheticTables.SimpleGlyph(0, 0, 500, 700),
            SyntheticTables.SimpleGlyph(0, 0, 600, 712),
            SyntheticTables.SimpleGlyph(0, 0, 500, 520));
        OpenTypeFont font = SyntheticFont.Minimal()
            .With("head", SyntheticTables.Head(indexToLocFormat: 1))
            .With("cmap", SyntheticTables.Cmap((3, 1, SyntheticTables.Format4(('H', 1), ('x', 2)))))
            .With("glyf", glyf)
            .With("loca", loca)
            .Load();

        Assert.Equal(712, font.Descriptor.CapHeight);
        Assert.Equal(520, font.Descriptor.XHeight);
    }

    [Fact]
    public void FallsBackToTheAscentForCapHeightWithoutAGlyphToMeasure()
    {
        OpenTypeFont font = SyntheticFont.Minimal().Load();

        Assert.Equal(800, font.Descriptor.CapHeight);
        Assert.Equal(0, font.Descriptor.XHeight);
        Assert.Equal(0, font.Descriptor.AverageWidth);
        Assert.Equal(0f, font.Descriptor.ItalicAngle);
        Assert.Equal(700, font.Descriptor.MaxWidth);
        Assert.Equal(500, font.Descriptor.MissingWidth);
    }

    [Fact]
    public void FallsBackToTheAscentForCapHeightWhenItsGlyphIsEmpty()
    {
        (byte[] glyf, byte[] loca) = SyntheticTables.GlyphData(
            longOffsets: false, SyntheticTables.SimpleGlyph(50, 0, 450, 700), [], []);
        OpenTypeFont font = SyntheticFont.Minimal()
            .With("cmap", SyntheticTables.Cmap((3, 1, SyntheticTables.Format4(('H', 1), ('x', 2)))))
            .With("glyf", glyf)
            .With("loca", loca)
            .Load();

        Assert.Equal(800, font.Descriptor.CapHeight);
        Assert.Equal(0, font.Descriptor.XHeight);
    }

    [Fact]
    public void FallsBackToTheAscentForCapHeightInACffFontWhoseOs2LacksIt()
    {
        // CFF outlines are not read for their bounds, so an "H" and an "x" the font has are no help.
        SyntheticFont font = SyntheticFont.Minimal().Without("glyf").Without("loca")
            .With("CFF ", SyntheticLayout.Cff("Test", 3))
            .With("cmap", SyntheticTables.Cmap((3, 1, SyntheticTables.Format4(('H', 1), ('x', 2)))));
        font.Version = "OTTO";

        FontDescriptorInfo descriptor = font.Load().Descriptor;

        Assert.Equal(800, descriptor.CapHeight);
        Assert.Equal(0, descriptor.XHeight);
    }

    [Fact]
    public void ScalesTheStemEstimateToTheEm()
    {
        OpenTypeFont font = SyntheticFont.Minimal().With("head", SyntheticTables.Head(unitsPerEm: 2048)).Load();

        // 50 + (400 / 65)² = 87.87 thousandths of an em.
        Assert.Equal(180, font.Descriptor.StemV);
        Assert.Equal(500f, font.Descriptor.ToGlyphSpace(1024));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(16385)]
    public void RejectsUnitsPerEmOutsideTheSpecification(int unitsPerEm)
    {
        byte[] font = SyntheticFont.Minimal().With("head", SyntheticTables.Head(unitsPerEm: unitsPerEm)).Build();

        Assert.Throws<FontFormatException>(() => OpenTypeFont.Load(font));
    }

    [Fact]
    public void RejectsAFontWithoutGlyphs()
    {
        byte[] font = SyntheticFont.Minimal().With("maxp", SyntheticTables.Maxp(0)).Build();

        Assert.Throws<FontFormatException>(() => OpenTypeFont.Load(font));
    }

    [Theory]
    [InlineData("head", 40)]
    [InlineData("hhea", 30)]
    [InlineData("maxp", 5)]
    public void RejectsTruncatedHeaderTables(string tag, int length)
    {
        SyntheticFont font = SyntheticFont.Minimal();
        byte[] table = tag switch
        {
            "head" => SyntheticTables.Head(),
            "hhea" => SyntheticTables.Hhea(numberOfHMetrics: 3),
            _ => SyntheticTables.Maxp(3)
        };

        Assert.Throws<FontFormatException>(() => font.With(tag, table.Take(length).ToArray()).Load());
    }
}
