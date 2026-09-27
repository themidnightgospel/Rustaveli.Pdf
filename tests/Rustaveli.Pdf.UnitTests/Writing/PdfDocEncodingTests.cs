using Rustaveli.Pdf.Writing;

namespace Rustaveli.Pdf.UnitTests.Writing;

public class PdfDocEncodingTests
{
    [Theory]
    [InlineData('\t', 0x09)]
    [InlineData('\n', 0x0A)]
    [InlineData('\r', 0x0D)]
    [InlineData(' ', 0x20)]
    [InlineData('A', 0x41)]
    [InlineData('~', 0x7E)]
    [InlineData('¡', 0xA1)]
    [InlineData('é', 0xE9)]
    [InlineData('¬', 0xAC)]
    [InlineData('®', 0xAE)]
    [InlineData('ÿ', 0xFF)]
    public void MapsAsciiAndLatin1ToThemselves(char character, int expected)
    {
        Assert.Equal(expected, PdfDocEncoding.Encode(character));
    }

    [Theory]
    [InlineData('˘', 0x18)]
    [InlineData('ˇ', 0x19)]
    [InlineData('ˆ', 0x1A)]
    [InlineData('˙', 0x1B)]
    [InlineData('˝', 0x1C)]
    [InlineData('˛', 0x1D)]
    [InlineData('˚', 0x1E)]
    [InlineData('˜', 0x1F)]
    [InlineData('•', 0x80)]
    [InlineData('†', 0x81)]
    [InlineData('‡', 0x82)]
    [InlineData('…', 0x83)]
    [InlineData('—', 0x84)]
    [InlineData('–', 0x85)]
    [InlineData('ƒ', 0x86)]
    [InlineData('⁄', 0x87)]
    [InlineData('‹', 0x88)]
    [InlineData('›', 0x89)]
    [InlineData('−', 0x8A)]
    [InlineData('‰', 0x8B)]
    [InlineData('„', 0x8C)]
    [InlineData('“', 0x8D)]
    [InlineData('”', 0x8E)]
    [InlineData('‘', 0x8F)]
    [InlineData('’', 0x90)]
    [InlineData('‚', 0x91)]
    [InlineData('™', 0x92)]
    [InlineData('ﬁ', 0x93)]
    [InlineData('ﬂ', 0x94)]
    [InlineData('Ł', 0x95)]
    [InlineData('Œ', 0x96)]
    [InlineData('Š', 0x97)]
    [InlineData('Ÿ', 0x98)]
    [InlineData('Ž', 0x99)]
    [InlineData('ı', 0x9A)]
    [InlineData('ł', 0x9B)]
    [InlineData('œ', 0x9C)]
    [InlineData('š', 0x9D)]
    [InlineData('ž', 0x9E)]
    [InlineData('€', 0xA0)]
    public void MapsTypographicCharactersToTheirPdfDocEncodingCodes(char character, int expected)
    {
        Assert.Equal(expected, PdfDocEncoding.Encode(character));
    }

    [Theory]
    [InlineData('\u0000')]
    [InlineData('\u0008')]
    [InlineData('\u000B')]
    [InlineData('\u000C')]
    [InlineData('\u001F')]
    [InlineData('\u007F')]
    [InlineData('\u0080')]
    [InlineData('\u009F')]
    [InlineData(' ')]
    [InlineData('­')]
    [InlineData('Ā')]
    [InlineData('ა')]
    [InlineData('中')]
    [InlineData('\uD834')]
    public void ReportsCharactersWithoutACode(char character)
    {
        Assert.Equal(-1, PdfDocEncoding.Encode(character));
    }
}
