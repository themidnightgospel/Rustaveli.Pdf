using Rustaveli.Pdf.Fonts.Substitution;

namespace Rustaveli.Pdf.UnitTests.Shaping;

public class ScriptDetectionTests
{
    public static TheoryData<int, string> Letters => new TheoryData<int, string>
    {
        { 'A', "latn" }, { 'Z', "latn" }, { 'a', "latn" }, { 'z', "latn" }, { 0x00C0, "latn" }, { 0x00D6, "latn" },
        { 0x00D8, "latn" }, { 0x00F6, "latn" }, { 0x00F8, "latn" }, { 0x024F, "latn" }, { 0x1E00, "latn" },
        { 0x1EFF, "latn" }, { 0x2C60, "latn" }, { 0x2C7F, "latn" }, { 0xA720, "latn" }, { 0xA7FF, "latn" },
        { 0xAB30, "latn" }, { 0xAB6F, "latn" }, { 0xFB00, "latn" }, { 0xFB06, "latn" }, { 0xFF21, "latn" },
        { 0xFF3A, "latn" }, { 0xFF41, "latn" }, { 0xFF5A, "latn" },
        { 0x0370, "grek" }, { 0x03FF, "grek" }, { 0x1F00, "grek" }, { 0x1FFF, "grek" },
        { 0x0400, "cyrl" }, { 0x052F, "cyrl" }, { 0x1C80, "cyrl" }, { 0x1C8F, "cyrl" }, { 0x2DE0, "cyrl" },
        { 0x2DFF, "cyrl" }, { 0xA640, "cyrl" }, { 0xA69F, "cyrl" },
        { 0x0530, "armn" }, { 0x058F, "armn" }, { 0xFB13, "armn" }, { 0xFB17, "armn" },
        { 0x0590, "hebr" }, { 0x05FF, "hebr" }, { 0xFB1D, "hebr" }, { 0xFB4F, "hebr" },
        { 0x0600, "arab" }, { 0x06FF, "arab" }, { 0x0750, "arab" }, { 0x077F, "arab" }, { 0x08A0, "arab" },
        { 0x08FF, "arab" }, { 0xFB50, "arab" }, { 0xFDFF, "arab" }, { 0xFE70, "arab" }, { 0xFEFF, "arab" },
        { 0x10A0, "geor" }, { 0x10FF, "geor" }, { 0x1C90, "geor" }, { 0x1CBF, "geor" }, { 0x2D00, "geor" },
        { 0x2D2F, "geor" },
    };

    [Theory]
    [MemberData(nameof(Letters))]
    public void RecognisesTheScriptsFontsTreatApart(int codepoint, string script) =>
        Assert.Equal(ScriptTag.Parse(script), ScriptDetection.Of(codepoint));

    [Theory]
    [InlineData('0')]
    [InlineData(' ')]
    [InlineData('.')]
    [InlineData('@')]
    [InlineData('[')]
    [InlineData('`')]
    [InlineData('{')]
    [InlineData(0x00D7)]
    [InlineData(0x00F7)]
    [InlineData(0x00BF)]
    [InlineData(0x0250)]
    [InlineData(0x4E16)]
    [InlineData(0x1F600)]
    public void CharactersEveryScriptSharesOrUnknownOnesHaveNone(int codepoint) =>
        Assert.Null(ScriptDetection.Of(codepoint));

    [Fact]
    public void TextTakesTheScriptOfItsFirstLetter()
    {
        Assert.Equal(ScriptTag.Greek, ScriptDetection.Of("12, αβγ abc".AsSpan()));
        Assert.Equal(ScriptTag.Latin, ScriptDetection.Of("abc αβγ".AsSpan()));
    }

    [Fact]
    public void TextWithoutLettersHasTheDefaultScript()
    {
        Assert.Equal(ScriptTag.Default, ScriptDetection.Of("123 !?".AsSpan()));
        Assert.Equal(ScriptTag.Default, ScriptDetection.Of(ReadOnlySpan<char>.Empty));
    }

    [Fact]
    public void ACharacterBeyondTheBasicPlaneIsReadWhole()
    {
        // U+1D400 is a mathematical bold capital A: a letter of no script here, read as one character, not two halves.
        Assert.Equal(ScriptTag.Latin, ScriptDetection.Of("\U0001D400a".AsSpan()));
    }

    [Fact]
    public void ALoneSurrogateIsNoLetter() =>
        Assert.Equal(ScriptTag.Hebrew, ScriptDetection.Of("\uD800א".AsSpan()));

    [Fact]
    public void AHighSurrogateEndingTheTextIsNoLetter() =>
        Assert.Equal(ScriptTag.Default, ScriptDetection.Of("1\uD800".AsSpan()));
}
