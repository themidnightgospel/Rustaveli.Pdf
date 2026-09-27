using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class PdfNameTests
{
    [Theory]
    [InlineData("Type", "/Type")]
    [InlineData("", "/")]
    [InlineData("A;B_C-1.5", "/A;B_C-1.5")]
    [InlineData("!", "/!")]
    [InlineData("~", "/~")]
    [InlineData("A B", "/A#20B")]
    [InlineData("a#b", "/a#23b")]
    [InlineData("()<>[]{}/%", "/#28#29#3C#3E#5B#5D#7B#7D#2F#25")]
    [InlineData("\u0001\t\n\u001F", "/#01#09#0A#1F")]
    [InlineData("\u007F", "/#7F")]
    [InlineData("é", "/#C3#A9")]
    [InlineData("ა", "/#E1#83#90")]
    public void EscapesEveryByteThatIsNotAPlainNameCharacter(string value, string expected)
    {
        PdfName name = new PdfName(value);

        Assert.Equal(expected, Latin1.Text(name.Encoded));
        Assert.Equal(expected, name.ToString());
        Assert.Equal(value, name.Value);
    }

    [Fact]
    public void RoundTripsThroughAnIndependentParser()
    {
        string value = "Name with (delimiters) #, spaces and ünïcödé";

        object? parsed = PdfSyntaxParser.ParseSingle(new PdfName(value).Encoded.ToArray());

        Assert.Equal(new ParsedName(value), parsed);
    }

    [Theory]
    [InlineData("a\0b")]
    [InlineData("\0a")]
    [InlineData("\0")]
    public void RejectsTheNullCharacter(string value)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new PdfName(value));

        Assert.Equal("value", exception.ParamName);
        Assert.StartsWith("A PDF name cannot contain the null character.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new PdfName(null!));
    }

    [Fact]
    public void EqualsAnyNameWithTheSameValue()
    {
        PdfName first = new PdfName("Font");
        PdfName second = new PdfName("Font");

        Assert.True(first.Equals(second));
        Assert.True(first.Equals((object)second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void DiffersFromNamesWithOtherValuesAndFromOtherTypes()
    {
        PdfName name = new PdfName("Font");

        Assert.False(name.Equals(new PdfName("font")));
        Assert.False(name.Equals((PdfName?)null));
        Assert.False(name.Equals("Font"));
        Assert.False(name.Equals((object?)null));
    }
}
