using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

public class NameTableTests
{
    private static FontNames Read(
        params (int Platform, int Encoding, int Language, int NameId, byte[] Bytes)[] records) =>
        NameTable.Read(SyntheticTables.Name(records));

    [Fact]
    public void PrefersWindowsUnitedStatesEnglish()
    {
        FontNames names = Read(
            SyntheticTables.NameRecord(1, 0, 0, 1, "Mac Family"),
            SyntheticTables.NameRecord(3, 1, 0x0407, 1, "Deutsche Familie"),
            SyntheticTables.NameRecord(3, 1, 0x0809, 1, "British Family"),
            SyntheticTables.NameRecord(3, 1, 0x0409, 1, "American Family"),
            SyntheticTables.NameRecord(0, 3, 0, 1, "Unicode Family"));

        Assert.Equal("American Family", names.Family);
        Assert.Equal(
            new[] { "Mac Family", "Deutsche Familie", "British Family", "American Family", "Unicode Family" },
            names.FamilyAliases);
    }

    [Theory]
    [InlineData(0x0809, "British")]
    [InlineData(0x0407, "Unicode")]
    public void RanksOtherEnglishAboveTheUnicodePlatformAndThatAboveOtherLanguages(int language, string expected)
    {
        FontNames names = Read(
            SyntheticTables.NameRecord(3, 1, 0x0407, 4, "German"),
            SyntheticTables.NameRecord(0, 4, 0, 4, "Unicode"),
            SyntheticTables.NameRecord(3, 1, language, 4, language == 0x0809 ? "British" : "German again"));

        Assert.Equal(expected, names.FullName);
    }

    [Fact]
    public void RanksMacEnglishAboveOtherWindowsLanguages()
    {
        FontNames names = Read(
            SyntheticTables.NameRecord(1, 0, 2, 2, "Deutsch Mac"),
            SyntheticTables.NameRecord(3, 1, 0x0407, 2, "Standard"),
            SyntheticTables.NameRecord(1, 0, 0, 2, "Regular"));

        Assert.Equal("Regular", names.Subfamily);
    }

    [Fact]
    public void DecodesMacRoman()
    {
        byte[] cafe = [(byte)'C', (byte)'a', (byte)'f', 0x8E, 0xA5];
        FontNames names = Read((1, 0, 0, 1, cafe));

        Assert.Equal("Café•", names.Family);
    }

    [Fact]
    public void ReadsWindowsSymbolAndFullRepertoireNamesAsUtf16()
    {
        FontNames names = Read(
            SyntheticTables.NameRecord(3, 0, 0x0409, 1, "Symbol Family"),
            SyntheticTables.NameRecord(3, 10, 0x0409, 6, "Full-Repertoire"));

        Assert.Equal("Symbol Family", names.Family);
        Assert.Equal("Full-Repertoire", names.PostScriptName);
    }

    [Fact]
    public void SkipsRecordsItCannotDecodeOrLocate()
    {
        FontNames names = Read(
            (3, 2, 0x0411, 1, [0x82, 0xA0]),
            (1, 1, 11, 1, [0x82, 0xA0]),
            (0, 5, 0, 1, [0, 65]),
            (2, 0, 0, 1, [65]),
            (3, 1, 0x0409, 1, []),
            (3, 1, 0x0409, 300, [0, 65]),
            SyntheticTables.NameRecord(3, 1, 0x0409, 2, "Bold"));

        Assert.Equal(string.Empty, names.Family);
        Assert.Equal("Bold", names.Subfamily);
        Assert.Empty(names.FamilyAliases);
    }

    [Fact]
    public void SkipsARecordPointingPastTheTable()
    {
        byte[] table = SyntheticTables.Name(
            SyntheticTables.NameRecord(3, 1, 0x0409, 1, "Lost"),
            SyntheticTables.NameRecord(3, 1, 0x0409, 2, "Kept"));

        // The first record's offset is its string's position in storage; move it far past the end.
        BigEndian.WriteUInt16(table, 6 + 10, 0xF000);

        FontNames names = NameTable.Read(table);

        Assert.Equal(string.Empty, names.Family);
        Assert.Equal("Kept", names.Subfamily);
    }

    [Fact]
    public void DropsAnOddTrailingByteAndTrailingNulls()
    {
        byte[] bytes = [.. new FontBytes().Utf16("Name\0").ToArray(), 0x41];

        Assert.Equal("Name", Read((3, 1, 0x0409, 1, bytes)).Family);
    }

    [Fact]
    public void RejectsRecordsPastTheTable()
    {
        byte[] table = new FontBytes().U16(0).U16(40).U16(0).ToArray();

        Assert.Throws<FontFormatException>(() => NameTable.Read(table));
    }

    [Fact]
    public void PrefersTheTypographicFamily()
    {
        FontNames names = TestFonts.SpecimenSemiBold.Names;

        Assert.Equal("Specimen Sans SemiBold", names.Family);
        Assert.Equal("Regular", names.Subfamily);
        Assert.Equal("Specimen Sans", names.TypographicFamily);
        Assert.Equal("SemiBold", names.TypographicSubfamily);
        Assert.Equal("Specimen Sans", names.PreferredFamily);
        Assert.Equal("SemiBold", names.PreferredSubfamily);
        Assert.Equal(new[] { "Specimen Sans" }, names.PreferredFamilyAliases);
        Assert.Equal(new[] { "Specimen Sans SemiBold" }, names.FamilyAliases);
        Assert.Equal("SpecimenSans-SemiBold", names.PostScriptName);
    }

    [Fact]
    public void ReadsAFontNamedOnlyOnTheMacintoshPlatform()
    {
        FontNames names = TestFonts.SpecimenItalic.Names;

        Assert.Equal("Specimen Sans", names.Family);
        Assert.Equal("Italic", names.Subfamily);
        Assert.Equal("Specimen Sans Italic", names.FullName);
        Assert.Equal("SpecimenSans-Italic", names.PostScriptName);
    }

    [Theory]
    [InlineData("My Font (Bold)/[x]{y}<z>%", "MyFontBoldxyz")]
    [InlineData("  ", "Font")]
    [InlineData("Élégante-Regular", "lgante-Regular")]
    public void SanitisesThePostScriptName(string name, string expected)
    {
        Assert.Equal(expected, Read(SyntheticTables.NameRecord(3, 1, 0x0409, 6, name)).PostScriptName);
    }

    [Fact]
    public void LimitsThePostScriptNameToSixtyThreeCharacters()
    {
        string name = new string('N', 80);

        Assert.Equal(63, Read(SyntheticTables.NameRecord(3, 1, 0x0409, 6, name)).PostScriptName.Length);
    }

    [Theory]
    [InlineData("Family", "Bold", "Family-Bold")]
    [InlineData("Family", "", "Family")]
    [InlineData("", "", "Font")]
    public void DerivesAMissingPostScriptNameFromTheFamily(string family, string subfamily, string expected)
    {
        List<(int, int, int, int, byte[])> records = [];

        if (family.Length > 0)
            records.Add(SyntheticTables.NameRecord(3, 1, 0x0409, 1, family));

        if (subfamily.Length > 0)
            records.Add(SyntheticTables.NameRecord(3, 1, 0x0409, 2, subfamily));

        Assert.Equal(expected, Read([.. records]).PostScriptName);
    }

    [Fact]
    public void GivesAFontWithoutANameTableEmptyNames()
    {
        OpenTypeFont font = SyntheticFont.Minimal().Load();

        Assert.Same(FontNames.None, font.Names);
        Assert.Equal("Font", font.Names.PostScriptName);
        Assert.Empty(font.Names.PreferredFamilyAliases);
    }

    [Fact]
    public void EncodesAndDecodesMacRoman()
    {
        Assert.Equal(0x41, MacRoman.Encode('A'));
        Assert.Equal(0x8E, MacRoman.Encode('é'));
        Assert.Equal(0xF0, MacRoman.Encode(''));
        Assert.Equal(-1, MacRoman.Encode('一'));
        Assert.Equal(-1, MacRoman.Encode(-1));
        Assert.Equal(-1, MacRoman.Encode(0x1F600));
        Assert.Equal('ˇ', MacRoman.ToUnicode(0xFF));
    }
}
