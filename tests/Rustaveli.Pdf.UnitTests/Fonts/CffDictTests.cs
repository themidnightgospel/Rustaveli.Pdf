using Rustaveli.Pdf.Fonts;

namespace Rustaveli.Pdf.UnitTests.Fonts;

/// <summary>CFF DICTs read as entries: every operand encoding, real numbers, and where each entry's bytes lie.</summary>
public class CffDictTests
{
    [Fact]
    public void EveryOperandEncodingIsRead()
    {
        byte[] dict =
        [
            139, 239, 39, 247, 0, 251, 0, 250, 255, 254, 255, 5,
            28, 0x7F, 0xFF, 29, 0x00, 0x01, 0x00, 0x00, 17,
        ];

        List<CffDictEntry> entries = CffDict.Read(dict);

        Assert.Equal([0d, 100, -100, 108, -108, 1131, -1131], entries[0].Operands);
        Assert.Equal(5, entries[0].Operator);
        Assert.Equal([32767d, 65536], entries[1].Operands);
        Assert.Equal((12, 9), (entries[1].Start, entries[1].Length));
    }

    [Fact]
    public void RealNumbersAreRead()
    {
        // -2.25, 0.001, 1.5E3 and 1E-2, then FontMatrix (12 7).
        byte[] dict = [30, 0xE2, 0xA2, 0x5F, 30, 0x0A, 0x00, 0x1F, 30, 0x1A, 0x5B, 0x3F, 30, 0x1C, 0x2F, 12, 7];

        CffDictEntry entry = Assert.Single(CffDict.Read(dict));

        Assert.Equal((12 << 8) | 7, entry.Operator);
        Assert.Equal([-2.25, 0.001, 1500, 0.01], entry.Operands);
        Assert.Equal(dict.Length, entry.Length);
    }

    [Fact]
    public void AnIntegerOperandIsReadAsOne()
    {
        CffDictEntry entry = Assert.Single(CffDict.Read([139 + 7, 139 + 9, 18]));

        Assert.Equal(7, entry.Integer());
        Assert.Equal(9, entry.Integer(1));
        Assert.Throws<FontFormatException>(() => entry.Integer(2));
        Assert.Throws<FontFormatException>(() => Assert.Single(CffDict.Read([30, 0x1A, 0x5F, 18])).Integer());

        // 1E10, a whole number too large for an integer: cast, it would come back as some other number.
        Assert.Throws<FontFormatException>(() => Assert.Single(CffDict.Read([30, 0x1B, 0x10, 0xFF, 18])).Integer());
    }

    [Theory]
    [InlineData(new byte[] { 255, 17 })]
    [InlineData(new byte[] { 30, 0xD1, 0xFF, 17 })]
    [InlineData(new byte[] { 30, 0xAA, 0xFF, 17 })]
    [InlineData(new byte[] { 28, 0 })]
    public void AMalformedDictIsRefused(byte[] dict) => Assert.Throws<FontFormatException>(() => CffDict.Read(dict));

    [Fact]
    public void ADictWithMoreOperandsThanCffAllowsIsRefused() =>
        Assert.Throws<FontFormatException>(() => CffDict.Read([.. Enumerable.Repeat((byte)139, 49), 17]));
}
