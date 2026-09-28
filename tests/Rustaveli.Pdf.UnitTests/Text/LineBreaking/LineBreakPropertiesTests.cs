using Rustaveli.Pdf.Text.LineBreaking;

namespace Rustaveli.Pdf.UnitTests.LineBreaking;

public class LineBreakPropertiesTests
{
    [Fact]
    public void ClassifiesEveryCodePointAsTheUnicodeDataFilesDo()
    {
        string[] lineBreak = UnicodeData.Property("LineBreak.txt", "XX");
        string[] category = UnicodeData.Property("DerivedGeneralCategory.txt", "Cn");
        string[] width = UnicodeData.Property("EastAsianWidth.txt", "N");
        bool[] pictographic = UnicodeData.BinaryProperty("emoji-data.txt", "Extended_Pictographic");
        List<string> mismatches = [];

        for (int codepoint = 0; codepoint < UnicodeData.CodepointCount && mismatches.Count < 20; codepoint++)
        {
            LineBreakClass expected = Resolve(lineBreak[codepoint], category[codepoint]);
            bool eastAsian = width[codepoint] is "F" or "W" or "H";
            bool unassignedPictographic = pictographic[codepoint] && category[codepoint] == "Cn";
            LineBreakProperties actual = LineBreakProperties.Of(codepoint);

            if (actual.Class != expected || actual.IsEastAsian != eastAsian ||
                actual.IsUnassignedPictographic != unassignedPictographic)
            {
                mismatches.Add(
                    $"U+{codepoint:X4}: expected {expected}, east Asian {eastAsian}, unassigned pictographic " +
                    $"{unassignedPictographic}; got {actual.Class}, {actual.IsEastAsian}, {actual.IsUnassignedPictographic}");
            }
        }

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }

    [Fact]
    public void IsBuiltFromTheUnicodeVersionOfTheTestData()
    {
        Assert.Equal("16.0.0", LineBreakTable.UnicodeVersion);
        Assert.StartsWith("# LineBreak-16.0.0.txt", File.ReadLines(UnicodeData.PathOf("LineBreak.txt")).First());
    }

    [Theory]
    [InlineData(0x0061, "AL")]
    [InlineData(0x0020, "SP")]
    [InlineData(0x00A0, "GL")]
    [InlineData(0x00AB, "QUPi")]
    [InlineData(0x00BB, "QUPf")]
    [InlineData(0x0022, "QU")]
    [InlineData(0x017F, "AL")]
    [InlineData(0x0180, "AL")]
    [InlineData(0x05D0, "HL")]
    [InlineData(0x4E2D, "ID")]
    [InlineData(0x1F44D, "EB")]
    [InlineData(0x10FFFF, "AL")]
    public void ReadsTheClassOnEitherSideOfTheDirectlyIndexedRange(int codepoint, string expected)
    {
        Assert.Equal(expected, LineBreakProperties.Of(codepoint).Class.ToString());
    }

    [Theory]
    [InlineData(0x0E01, "AL")]
    [InlineData(0x0E31, "CM")]
    [InlineData(0x3041, "NS")]
    [InlineData(0x00B1, "PR")]
    [InlineData(0xD800, "AL")]
    [InlineData(0xE000, "AL")]
    [InlineData(0x0378, "AL")]
    public void ResolvesTheClassesThatDependOnContextAsTheDefaultRulesDo(int codepoint, string expected)
    {
        // Thai letters are complex-context and become letters, their vowel marks combining marks; small kana are
        // conditional Japanese starters and become nonstarters; surrogates, private use and unassigned code points
        // are letters.
        Assert.Equal(expected, LineBreakProperties.Of(codepoint).Class.ToString());
    }

    [Fact]
    public void MarksEastAsianWidthAndUnassignedPictographs()
    {
        // Fullwidth and ASCII left parentheses; an unassigned code point among the emoji; thumbs up.
        Assert.True(LineBreakProperties.Of(0xFF08).IsEastAsian);
        Assert.False(LineBreakProperties.Of(0x0028).IsEastAsian);
        Assert.True(LineBreakProperties.Of(0x1FAFF).IsUnassignedPictographic);
        Assert.False(LineBreakProperties.Of(0x1F44D).IsUnassignedPictographic);
    }

    [Fact]
    public void GivesAnOrphanedMarkThePropertiesOfALetter()
    {
        LineBreakProperties letter = LineBreakProperties.Alphabetic;

        Assert.Equal(LineBreakClass.AL, letter.Class);
        Assert.False(letter.IsEastAsian);
        Assert.False(letter.IsUnassignedPictographic);
    }

    [Fact]
    public void RepresentsTheEdgesOfTheText()
    {
        Assert.Equal(LineBreakClass.Eot, LineBreakProperties.EndOfText.Class);
        Assert.Equal(LineBreakClass.Sot, default(LineBreakProperties).Class);
        Assert.False(LineBreakProperties.EndOfText.IsEastAsian);
    }

    private static LineBreakClass Resolve(string value, string category) => value switch
    {
        "AI" or "SG" or "XX" => LineBreakClass.AL,
        "SA" => category is "Mn" or "Mc" ? LineBreakClass.CM : LineBreakClass.AL,
        "CJ" => LineBreakClass.NS,
        "QU" when category == "Pi" => LineBreakClass.QUPi,
        "QU" when category == "Pf" => LineBreakClass.QUPf,
        _ => (LineBreakClass)Enum.Parse(typeof(LineBreakClass), value)
    };
}
