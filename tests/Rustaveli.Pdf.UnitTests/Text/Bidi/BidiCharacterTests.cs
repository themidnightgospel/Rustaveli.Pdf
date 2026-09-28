using Rustaveli.Pdf.Text.Bidi;

namespace Rustaveli.Pdf.UnitTests.Bidi;

/// <summary>
/// The generated property tables, checked code point by code point against the Unicode data files they were built
/// from, read here by a parser of their own.
/// </summary>
public class BidiCharacterTests
{
    private const int CodeSpace = 0x110000;

    [Fact]
    public void GivesEveryCodePointTheClassTheDatabaseDerives()
    {
        BidiClass[] expected = DerivedClasses();
        List<string> mismatches = new List<string>();

        for (int codepoint = 0; codepoint < CodeSpace; codepoint++)
        {
            BidiClass actual = BidiCharacter.ClassOf(codepoint);
            if (actual != expected[codepoint])
                mismatches.Add($"U+{codepoint:X4}: {actual}, expected {expected[codepoint]}");
        }

        Assert.Empty(mismatches.Take(20));
    }

    [Theory]
    [InlineData(0x0041, nameof(BidiClass.L))]
    [InlineData(0x05D0, nameof(BidiClass.R))]
    [InlineData(0x0627, nameof(BidiClass.AL))]
    [InlineData(0x0031, nameof(BidiClass.EN))]
    [InlineData(0x002B, nameof(BidiClass.ES))]
    [InlineData(0x0025, nameof(BidiClass.ET))]
    [InlineData(0x0661, nameof(BidiClass.AN))]
    [InlineData(0x002C, nameof(BidiClass.CS))]
    [InlineData(0x0301, nameof(BidiClass.NSM))]
    [InlineData(0x200D, nameof(BidiClass.BN))]
    [InlineData(0x2029, nameof(BidiClass.B))]
    [InlineData(0x0009, nameof(BidiClass.S))]
    [InlineData(0x0020, nameof(BidiClass.WS))]
    [InlineData(0x0021, nameof(BidiClass.ON))]
    [InlineData(0x202A, nameof(BidiClass.LRE))]
    [InlineData(0x202D, nameof(BidiClass.LRO))]
    [InlineData(0x202B, nameof(BidiClass.RLE))]
    [InlineData(0x202E, nameof(BidiClass.RLO))]
    [InlineData(0x202C, nameof(BidiClass.PDF))]
    [InlineData(0x2066, nameof(BidiClass.LRI))]
    [InlineData(0x2067, nameof(BidiClass.RLI))]
    [InlineData(0x2068, nameof(BidiClass.FSI))]
    [InlineData(0x2069, nameof(BidiClass.PDI))]
    [InlineData(0x10FFFF, nameof(BidiClass.BN))]
    [InlineData(0x1E900, nameof(BidiClass.R))]
    [InlineData(0x07FF, nameof(BidiClass.R))]
    [InlineData(0x08FF, nameof(BidiClass.NSM))]
    public void ClassifiesARepresentativeOfEachClass(int codepoint, string expected) =>
        Assert.Equal(expected, BidiCharacter.ClassOf(codepoint).ToString());

    [Theory]
    [InlineData(-1)]
    [InlineData(0x110000)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void TakesValuesOutsideTheCodeSpaceAsLeftToRight(int value) =>
        Assert.Equal(BidiClass.L, BidiCharacter.ClassOf(value));

    [Fact]
    public void FindsNothingRightToLeftBeforeTheHebrewBlock()
    {
        // The paragraph's scan for plain left-to-right text looks nothing up below U+0590, which this makes safe.
        for (int codepoint = 0; codepoint < 0x0590; codepoint++)
        {
            BidiClass type = BidiCharacter.ClassOf(codepoint);
            Assert.False(
                type is BidiClass.R or BidiClass.AL or BidiClass.AN || type >= BidiClass.LRE,
                $"U+{codepoint:X4} is {type}");
        }

        Assert.Equal(BidiClass.NSM, BidiCharacter.ClassOf(0x0591));
    }

    [Fact]
    public void MirrorsEveryCharacterTheDatabaseGivesAMirroringGlyph()
    {
        Dictionary<int, int> mirrors = UnicodeFiles.DataLines("BidiMirroring.txt")
            .ToDictionary(fields => UnicodeFiles.Hex(fields[0]), fields => UnicodeFiles.Hex(fields[1]));

        Assert.Equal(428, mirrors.Count);
        foreach (KeyValuePair<int, int> mirror in mirrors)
            Assert.Equal(mirror.Value, BidiCharacter.Mirror(mirror.Key));
    }

    [Theory]
    [InlineData('(', ')')]
    [InlineData(')', '(')]
    [InlineData('<', '>')]
    [InlineData('\u00AB', '\u00BB')]
    [InlineData('\u2264', '\u2265')]
    [InlineData('\uFF63', '\uFF62')]
    public void MirrorsPairedCharacters(char character, char mirror) =>
        Assert.Equal(mirror, BidiCharacter.Mirror(character));

    [Theory]
    [InlineData('A')]
    [InlineData('\u0000')]
    [InlineData('\u2211')] // Mirrored, but with no character to stand in: the font's own mirrored form is needed.
    [InlineData('\uFD3E')] // Ornate parentheses are exempt, for compatibility.
    [InlineData('\uFFFF')]
    [InlineData(0x1D6DB)]
    [InlineData(-1)]
    public void LeavesCharactersWithoutAMirroringGlyphAsTheyAre(int codepoint) =>
        Assert.Equal(codepoint, BidiCharacter.Mirror(codepoint));

    [Fact]
    public void PairsEveryBracketTheDatabaseLists()
    {
        List<string[]> brackets = UnicodeFiles.DataLines("BidiBrackets.txt").ToList();

        Assert.Equal(128, brackets.Count);
        foreach (string[] fields in brackets)
        {
            BidiBracketType expected = fields[2].Trim() == "o" ? BidiBracketType.Open : BidiBracketType.Close;
            BidiBracketType type = BidiCharacter.BracketOf(UnicodeFiles.Hex(fields[0]), out int pair);

            Assert.Equal(expected, type);
            Assert.Equal(UnicodeFiles.Hex(fields[1]), pair);
        }
    }

    [Theory]
    [InlineData('a')]
    [InlineData('<')]
    [InlineData('\u0000')]
    [InlineData('\uFFFF')]
    [InlineData(0x10000)]
    public void FindsNoBracketInOtherCharacters(int codepoint)
    {
        Assert.Equal(BidiBracketType.None, BidiCharacter.BracketOf(codepoint, out int pair));
        Assert.Equal(codepoint, pair);
    }

    [Fact]
    public void ComesFromTheUnicodeVersionOfTheDataFiles() =>
        Assert.StartsWith(
            $"# DerivedBidiClass-{BidiCharacterTables.UnicodeVersion}.txt",
            File.ReadLines(UnicodeFiles.PathOf("DerivedBidiClass.txt")).First());

    // DerivedBidiClass.txt: the @missing lines give the defaults, broadest first, and the data lines the rest.
    private static BidiClass[] DerivedClasses()
    {
        Dictionary<string, BidiClass> byName = new Dictionary<string, BidiClass>
        {
            ["Left_To_Right"] = BidiClass.L,
            ["Right_To_Left"] = BidiClass.R,
            ["Arabic_Letter"] = BidiClass.AL,
            ["European_Terminator"] = BidiClass.ET,
        };
        foreach (BidiClass type in Enum.GetValues(typeof(BidiClass)))
            byName[type.ToString()] = type;

        BidiClass[] classes = new BidiClass[CodeSpace];
        const string Missing = "# @missing:";

        foreach (string line in File.ReadLines(UnicodeFiles.PathOf("DerivedBidiClass.txt")))
        {
            if (!line.StartsWith(Missing, StringComparison.Ordinal))
                continue;

            string[] fields = line.Substring(Missing.Length).Split(';');
            (int first, int last) = UnicodeFiles.Range(fields[0]);
            classes.AsSpan(first, last - first + 1).Fill(byName[fields[1].Trim()]);
        }

        foreach (string[] fields in UnicodeFiles.DataLines("DerivedBidiClass.txt"))
        {
            (int first, int last) = UnicodeFiles.Range(fields[0]);
            classes.AsSpan(first, last - first + 1).Fill(byName[fields[1].Trim()]);
        }

        return classes;
    }
}
