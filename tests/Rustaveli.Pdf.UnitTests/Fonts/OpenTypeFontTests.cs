using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>
/// Parsing the committed Noto fonts. Every expected value was read from the same file with fontTools.
/// </summary>
public class OpenTypeFontTests
{
    [Fact]
    public void ReadsTheFontWideHeaders()
    {
        OpenTypeFont font = TestFonts.Regular;

        Assert.Equal(OutlineFormat.TrueType, font.Outlines);
        Assert.Equal(1000, font.UnitsPerEm);
        Assert.Equal(3884, font.GlyphCount);
        Assert.Equal(0, font.FaceIndex);
        Assert.Equal(new GlyphBounds(-621, -389, 2800, 1067), font.Descriptor.BoundingBox);
        Assert.Equal(1, font.Head.IndexToLocFormat);
        Assert.Equal(1069, font.HorizontalHeader.Ascender);
        Assert.Equal(-293, font.HorizontalHeader.Descender);
        Assert.Equal(3884, font.HorizontalHeader.NumberOfHMetrics);
    }

    [Fact]
    public void ReadsTheNames()
    {
        FontNames names = TestFonts.Regular.Names;

        Assert.Equal("Noto Sans", names.Family);
        Assert.Equal("Regular", names.Subfamily);
        Assert.Equal("Noto Sans Regular", names.FullName);
        Assert.Equal("NotoSans-Regular", names.PostScriptName);
        Assert.Null(names.TypographicFamily);
        Assert.Null(names.TypographicSubfamily);
        Assert.Equal("Noto Sans", names.PreferredFamily);
        Assert.Equal("Regular", names.PreferredSubfamily);
        Assert.Equal(new[] { "Noto Sans" }, names.FamilyAliases);
    }

    [Theory]
    [InlineData(' ', 3, 260)]
    [InlineData('A', 36, 639)]
    [InlineData('T', 55, 556)]
    [InlineData('V', 57, 600)]
    [InlineData('W', 58, 930)]
    [InlineData('a', 68, 561)]
    [InlineData('o', 82, 605)]
    [InlineData('.', 17, 268)]
    [InlineData('\u00C5', 135, 639)]
    public void MapsCharactersToGlyphsAndAdvances(char character, int glyph, int advance)
    {
        OpenTypeFont font = TestFonts.Regular;

        Assert.Equal(glyph, font.GetGlyphId(character));
        Assert.Equal(advance, font.GetAdvance((ushort)glyph));
        Assert.True(font.HasGlyph(character));
    }

    [Fact]
    public void ReportsCharactersTheFontLacksAsGlyphZero()
    {
        // Georgian is exactly what the Latin face lacks and the Georgian face has.
        Assert.Equal(0, TestFonts.Regular.GetGlyphId('\u10D0'));
        Assert.False(TestFonts.Regular.HasGlyph('\u10D0'));
        Assert.Equal(111, TestFonts.Georgian.GetGlyphId('\u10D0'));
        Assert.Equal(549, TestFonts.Georgian.GetAdvance(111));
        Assert.False(TestFonts.Georgian.HasGlyph('A'));
    }

    [Fact]
    public void ScalesAdvancesToThePointSize()
    {
        OpenTypeFont font = TestFonts.Regular;

        Assert.Equal(639 * 12f / 1000f, font.GetAdvance(36, 12f), 5);
        Assert.Equal(-70 * 10f / 1000f, font.GetKerning(55, 82, 10f), 5);
    }

    [Theory]
    [InlineData(TestFonts.RegularFile, 'A', 'V', -40)]
    [InlineData(TestFonts.RegularFile, 'V', 'A', -40)]
    [InlineData(TestFonts.RegularFile, 'T', 'o', -70)]
    [InlineData(TestFonts.RegularFile, 'A', 'T', -70)]
    [InlineData(TestFonts.RegularFile, 'L', 'T', -20)]
    [InlineData(TestFonts.RegularFile, 'Y', 'o', -50)]
    [InlineData(TestFonts.RegularFile, 'P', '.', -130)]
    [InlineData(TestFonts.RegularFile, 'a', 'b', 0)]
    [InlineData(TestFonts.BoldFile, 'A', 'V', -40)]
    [InlineData(TestFonts.ItalicFile, 'A', 'V', -10)]
    [InlineData(TestFonts.ItalicFile, 'L', 'T', -40)]
    [InlineData(TestFonts.GeorgianFile, '\u10D5', '\u10D0', -20)]
    public void KernsPairsAsTheFontsGposSays(string file, char left, char right, int expected)
    {
        OpenTypeFont font = file switch
        {
            TestFonts.BoldFile => TestFonts.Bold,
            TestFonts.ItalicFile => TestFonts.Italic,
            TestFonts.GeorgianFile => TestFonts.Georgian,
            _ => TestFonts.Regular
        };

        Assert.IsType<GlyphPositioningKerning>(font.Kerning);
        Assert.Equal(expected, font.GetKerning(font.GetGlyphId(left), font.GetGlyphId(right)));
    }

    [Fact]
    public void UsesTheTypographicLineMetricsTheFontAsksFor()
    {
        // Noto Sans sets USE_TYPO_METRICS, and its usWin metrics (1124/395) differ from the typographic ones.
        LineMetrics metrics = TestFonts.Regular.LineMetrics;

        Assert.Equal(new LineMetrics(1069, 293, 0, LineMetricsSource.Typographic), metrics);
        Assert.Equal(1362, metrics.LineHeight);
    }

    [Fact]
    public void ReadsTheStyleOfEachFace()
    {
        Assert.Equal(new FaceStyle(400, 5, FontSlant.Upright), TestFonts.Regular.Style);
        Assert.Equal(new FaceStyle(700, 5, FontSlant.Upright), TestFonts.Bold.Style);
        Assert.Equal(new FaceStyle(400, 5, FontSlant.Italic), TestFonts.Italic.Style);
    }

    [Fact]
    public void ReadsThePostTable()
    {
        PostTable regular = TestFonts.Regular.Post!;
        PostTable italic = TestFonts.Italic.Post!;

        Assert.Equal(0f, regular.ItalicAngle);
        Assert.Equal(-12f, italic.ItalicAngle);
        Assert.Equal(-100, regular.UnderlinePosition);
        Assert.Equal(50, regular.UnderlineThickness);
        Assert.False(regular.IsFixedPitch);
    }

    [Fact]
    public void DescribesTheFontForAPdfFontDescriptor()
    {
        FontDescriptorInfo descriptor = TestFonts.Regular.Descriptor;

        Assert.Equal("NotoSans-Regular", descriptor.FontName);
        Assert.Equal("Noto Sans", descriptor.FamilyName);
        Assert.Equal(1069, descriptor.Ascent);
        Assert.Equal(-293, descriptor.Descent);
        Assert.Equal(0, descriptor.Leading);
        Assert.Equal(714, descriptor.CapHeight);
        Assert.Equal(536, descriptor.XHeight);
        Assert.Equal(0f, descriptor.ItalicAngle);
        Assert.Equal(400, descriptor.Weight);
        Assert.Equal(5, descriptor.Width);
        Assert.Equal(88, descriptor.StemV);
        Assert.Equal(TestFonts.Regular.GetAdvance(0), descriptor.MissingWidth);
        Assert.Equal(1000, descriptor.UnitsPerEm);

        // Greek and Cyrillic put Noto Sans outside the standard Latin character set, which PDF calls symbolic.
        Assert.Equal(FontFlags.Symbolic, descriptor.Flags);
    }

    [Fact]
    public void FlagsItalicFacesAndEstimatesHeavierStems()
    {
        Assert.Equal(FontFlags.Symbolic | FontFlags.Italic, TestFonts.Italic.Descriptor.Flags);
        Assert.Equal(-12f, TestFonts.Italic.Descriptor.ItalicAngle);
        Assert.Equal(166, TestFonts.Bold.Descriptor.StemV);
    }

    [Fact]
    public void ConvertsFontUnitsToPdfGlyphSpace()
    {
        Assert.Equal(639f, TestFonts.Regular.Descriptor.ToGlyphSpace(639));
    }

    [Fact]
    public void ReportsUnrestrictedEmbedding()
    {
        FontEmbedding embedding = TestFonts.Regular.Embedding;

        Assert.Equal(0, embedding.FsType);
        Assert.True(embedding.IsInstallable);
        Assert.True(embedding.AllowsEmbedding);
        Assert.True(embedding.AllowsSubsetting);
    }

    [Fact]
    public void ReadsShortGlyphLocations()
    {
        // The Georgian face is small enough for 16-bit loca offsets; the Latin faces use 32-bit ones.
        OpenTypeFont font = TestFonts.Georgian;

        Assert.Equal(0, font.Head.IndexToLocFormat);
        Assert.Equal(225, font.GlyphCount);
        Assert.True(font.TryGetGlyphBounds(111, out GlyphBounds bounds));
        Assert.True(bounds.XMax > bounds.XMin);
    }

    [Fact]
    public void ReadsGlyphBoundsAndComposites()
    {
        OpenTypeFont font = TestFonts.Regular;
        List<GlyphComponent> components = [];

        font.Glyphs!.AddComponents(135, components);

        // Å is A (36) with a ring (335) above it.
        Assert.Equal(new[] { 36, 335 }, components.Select(component => (int)component.GlyphId));
        Assert.False(font.TryGetGlyphBounds(3, out _));
    }

    [Fact]
    public void ExposesRawTables()
    {
        OpenTypeFont font = TestFonts.Regular;

        Assert.True(font.TryGetTable(TableTag.Fpgm, out ReadOnlyMemory<byte> fpgm));
        Assert.True(fpgm.Length > 0);
        Assert.False(font.TryGetTable(TableTag.Kern, out _));
        Assert.Equal(font.FileData.Length, TestFonts.Bytes(TestFonts.RegularFile).Length);
    }
}
