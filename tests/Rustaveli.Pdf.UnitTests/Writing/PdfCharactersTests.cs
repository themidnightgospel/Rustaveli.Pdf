using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class PdfCharactersTests
{
    private const string Whitespace = "\0\t\n\f\r ";

    private const string Delimiters = "()<>[]{}/%";

    [Fact]
    public void ClassifiesEveryByteAsTheSpecificationDoes()
    {
        for (int value = 0; value < 256; value++)
        {
            byte current = (byte)value;
            bool whitespace = Whitespace.IndexOf((char)value) >= 0;
            bool delimiter = Delimiters.IndexOf((char)value) >= 0;

            Assert.Equal(whitespace, PdfCharacters.IsWhitespace(current));
            Assert.Equal(delimiter, PdfCharacters.IsDelimiter(current));
            Assert.Equal(!whitespace && !delimiter, PdfCharacters.IsRegular(current));
            Assert.Equal(value is >= 0x21 and <= 0x7E && value != '#' && !delimiter, PdfCharacters.IsNameCharacter(current));
        }
    }

    [Fact]
    public void WritesUppercaseHexDigitsForTheLowFourBits()
    {
        string digits = new string(Enumerable.Range(0, 16).Select(value => (char)PdfCharacters.HexDigit(value)).ToArray());

        Assert.Equal("0123456789ABCDEF", digits);
        Assert.Equal((byte)'F', PdfCharacters.HexDigit(0x1F));
        Assert.Equal((byte)'0', PdfCharacters.HexDigit(0xF0));
    }
}
