using System.IO.Compression;
using System.Text;
using Rustaveli.Pdf.Fonts;
using Rustaveli.Pdf.Output;
using Rustaveli.Pdf.UnitTests.Fonts;
using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Output;

public class EmbeddedFontTests
{
    /// <summary>The PDF objects that embed <paramref name="face"/>, having shown each glyph for its character.</summary>
    private static string Embed(OpenTypeFont face, params (ushort Glyph, char Character)[] shown)
    {
        using MemoryStream stream = new MemoryStream();
        using (PdfFileWriter file = new PdfFileWriter(stream, new PdfWriterOptions { CompressionLevel = CompressionLevel.NoCompression }))
        {
            EmbeddedFont font = new EmbeddedFont(face, file.Reserve());

            foreach ((ushort glyph, char character) in shown)
                font.CodeFor(new ShapedGlyph(face, glyph, character, 0, 1, 10, 0));

            font.Write(file);
            file.Finish(file.Write(new PdfDictionary()));
        }

        return Encoding.Latin1.GetString(stream.ToArray());
    }

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
    public void TheToUnicodeMapWritesALigaturesCharactersAndAGlyphStandingForNone()
    {
        string map = Encoding.ASCII.GetString(EmbeddedFont.ToUnicodeMap([((ushort)1, "ffi"), ((ushort)2, string.Empty)]));

        Assert.Contains("<0001> <006600660069>", map);

        // An empty destination: the glyph reads back as nothing rather than as the font's own idea of it.
        Assert.Contains("<0002> <>", map);
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

    [Fact]
    public void AFaceThatShowedOnlyMissingCharactersHasNoToUnicodeMap()
    {
        OpenTypeFont face = SyntheticFont.Minimal().Load();

        // .notdef stands for every missing character, so reads back as none of them, and a map of nothing is left out.
        Assert.DoesNotContain("/ToUnicode", Embed(face, (0, '\u4E16')), StringComparison.Ordinal);
        Assert.Contains("/ToUnicode", Embed(face, (0, '\u4E16'), (1, 'A')), StringComparison.Ordinal);
    }

    [Fact]
    public void AFaceWithoutAnXHeightWritesNone()
    {
        // Minimal has neither an OS/2 table nor an "x" to measure.
        OpenTypeFont withoutOne = SyntheticFont.Minimal().Load();
        OpenTypeFont withOne = SyntheticFont.Minimal().With("OS/2", SyntheticTables.Os2(xHeight: 480)).Load();

        Assert.DoesNotContain("/XHeight", Embed(withoutOne, (1, 'A')), StringComparison.Ordinal);
        Assert.Matches(@"/XHeight 480\b", Embed(withOne, (1, 'A')));
    }
}
