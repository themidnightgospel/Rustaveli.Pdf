using System.Text;
using Rustaveli.Pdf.Output;

namespace Rustaveli.Pdf.UnitTests.Output;

public class EmbeddedFontTests
{
    [Fact]
    public void BaseFontIsThePostScriptName()
    {
        Assert.Equal("NotoSans-Regular", EmbeddedFont.PostScriptName("NotoSans-Regular", "Noto Sans Regular"));
    }

    [Fact]
    public void AFaceWithoutAPostScriptNameIsNamedFromItsFullName()
    {
        Assert.Equal("NotoSansRegular", EmbeddedFont.PostScriptName("  ", "Noto Sans Regular"));
    }

    [Fact]
    public void CharactersAPostScriptNameCannotHoldAreDropped()
    {
        // Delimiters, spaces and anything outside printable ASCII would end or corrupt the name.
        Assert.Equal("ABC-Bold", EmbeddedFont.PostScriptName("A(B)C/<-%>[Bold]{ }é", string.Empty));
    }

    [Fact]
    public void ANameWithNothingLeftIsStillAName()
    {
        Assert.Equal("Font", EmbeddedFont.PostScriptName(string.Empty, "()"));
    }

    [Fact]
    public void TheToUnicodeMapWritesEachCodeAsUtf16()
    {
        string map = Encoding.ASCII.GetString(EmbeddedFont.ToUnicodeMap([(1, 'A'), (2, 0x1D400), (3, 0xD800), (4, -1)]));

        Assert.Contains("<0001> <0041>", map);

        // Beyond the Basic Multilingual Plane, a surrogate pair.
        Assert.Contains("<0002> <D835DC00>", map);

        // A lone surrogate or no character at all maps to the replacement character rather than invalid UTF-16.
        Assert.Contains("<0003> <FFFD>", map);
        Assert.Contains("<0004> <FFFD>", map);
        Assert.Contains("4 beginbfchar", map);
    }

    [Fact]
    public void TheToUnicodeMapSplitsIntoBlocksOfAHundred()
    {
        // A CMap's bfchar blocks may hold at most a hundred mappings each.
        (ushort, int)[] characters = Enumerable.Range(1, 250).Select(code => ((ushort)code, 'a' + (code % 26))).ToArray();

        string map = Encoding.ASCII.GetString(EmbeddedFont.ToUnicodeMap(characters));

        Assert.Equal(2, map.Split(["100 beginbfchar"], StringSplitOptions.None).Length - 1);
        Assert.Contains("50 beginbfchar", map);
        Assert.Equal(3, map.Split(["endbfchar"], StringSplitOptions.None).Length - 1);
    }
}
